using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SahaNet.Application.Alerts;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Application.Identity.Auth;
using SahaNet.Application.Weather;
using SahaNet.Domain.Geo;
using SahaNet.Domain.Identity;
using SahaNet.Domain.Notifications;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace SahaNet.IntegrationTests;

/// <summary>Gerçek PostGIS ve Redis konteynerleriyle API'yi ayağa kaldırır.</summary>
public sealed class SahaNetApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgis/postgis:16-3.4")
        .WithDatabase("sahanet")
        .WithUsername("sahanet")
        .WithPassword("sahanet_test")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    /// <summary>Gönderilen SMS'leri yakalar; testler OTP kodunu buradan okur.</summary>
    public CapturingSmsSender Sms { get; } = new();

    public ScriptedWeatherProvider Weather { get; } = new();

    public CapturingPushSender Push { get; } = new();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:ConnectionString", _postgres.GetConnectionString());
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Redis:ConnectionString", _redis.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        builder.UseSetting("PersonalData:EncryptionKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.UseSetting("PersonalData:LookupKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.UseSetting("RateLimiting:OtpPermitPerMinute", "1000");
        builder.UseSetting("BackgroundJobs:Enabled", "false");
        builder.UseSetting("Identity:AdminPhones:0", AdminPhone);

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ISmsSender>(Sms);
            services.AddSingleton<IPushSender>(Push);
            services.AddSingleton<IWeatherProvider>(Weather);
        });
    }

    /// <summary>Girişte yönetici rolü alan test numarası.</summary>
    public const string AdminPhone = "05000000001";

    /// <summary>Her test için benzersiz geçerli cep telefonu numarası.</summary>
    public static string NewPhone() => "05" + RandomNumberGenerator.GetInt32(100_000_000, 999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Giriş + zorunlu rızalar + balıkçı nişi ile kurulum (ilan verebilen kullanıcı).</summary>
    public async Task<(HttpClient Client, AuthResponse Auth)> LoginOnboardedAsync(string displayName = "Reis")
    {
        var (client, auth) = await LoginAsync();
        var documents = await client.GetFromJsonAsync<List<SahaNet.Application.Consents.ConsentDocumentResponse>>("/api/consents/documents");
        var decisions = documents!.Where(d => d.IsRequired).Select(d => new SahaNet.Application.Consents.ConsentDecision(d.Id, true)).ToList();
        (await client.PostAsJsonAsync("/api/me/consents", new SahaNet.Application.Consents.RecordConsentsCommand(decisions, "mobile"))).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/me/onboarding", new { nicheKey = "fishing", regionCode = "34", displayName })).EnsureSuccessStatusCode();
        return (client, auth);
    }

    /// <summary>OTP iste + doğrula; oturum açmış istemci döndürür.</summary>
    public async Task<(HttpClient Client, AuthResponse Auth)> LoginAsync(string? phone = null)
    {
        phone ??= NewPhone();
        var client = CreateClient();

        var request = await client.PostAsJsonAsync("/api/auth/otp/request", new { phone });
        request.EnsureSuccessStatusCode();

        var verify = await client.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code = Sms.LastCodeFor(phone) });
        verify.EnsureSuccessStatusCode();
        var auth = (await verify.Content.ReadFromJsonAsync<AuthResponse>())!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }
}

public sealed class CapturingSmsSender : ISmsSender
{
    private readonly ConcurrentDictionary<string, string> _lastMessageByPhone = new();

    public Task SendAsync(PhoneNumber to, string message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(to);
        _lastMessageByPhone[to.Value] = message;
        return Task.CompletedTask;
    }

    public string LastCodeFor(string phone)
    {
        var message = _lastMessageByPhone[PhoneNumber.Create(phone).Value.Value];
        return System.Text.RegularExpressions.Regex.Match(message, "[0-9]{6}").Value;
    }
}

/// <summary>Varsayılan sakin hava; fırtına yalnızca işaretlenen noktalara verilir.</summary>
public sealed class ScriptedWeatherProvider : IWeatherProvider
{
    private readonly ConcurrentDictionary<(double, double), bool> _stormy = new();

    public void MakeStormy(double latitude, double longitude) => _stormy[(latitude, longitude)] = true;

    public Task<WeatherForecast> GetForecastAsync(GeoPoint location, int days, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(location);
        var stormy = _stormy.ContainsKey((location.Latitude, location.Longitude));
        var now = DateTimeOffset.UtcNow;
        var start = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);
        var hours = Enumerable.Range(0, days * 24)
            .Select(i => stormy && i is >= 2 and <= 5
                ? new WeatherHour(start.AddHours(i), 70, 95, 220, 4.2, 230, 7)
                : new WeatherHour(start.AddHours(i), 12, 18, 220, 0.6, 230, 4))
            .ToList();
        return Task.FromResult(new WeatherForecast(location, now, hours));
    }
}

public sealed class CapturingPushSender : IPushSender
{
    public ConcurrentBag<(string Token, PushMessage Message)> Sent { get; } = [];

    public Task<PushResult> SendAsync(DeviceRegistration device, PushMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(device);
        Sent.Add((device.Token, message));
        return Task.FromResult(PushResult.Sent);
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<SahaNetApiFactory>
{
    public const string Name = "api";
}
