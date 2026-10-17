using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SahaNet.Api.Endpoints;
using SahaNet.Api.Health;
using SahaNet.Api.Hubs;
using SahaNet.Api.Infrastructure;
using SahaNet.Application;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Application.Market;
using SahaNet.Infrastructure;
using SahaNet.Infrastructure.Options;
using SahaNet.Infrastructure.Security;
using SahaNet.Modules.Fishing;
using SahaNet.Modules.Scrap;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.With<PhoneMaskingEnricher>());

builder.Services
    .AddApplication(typeof(FishingModule).Assembly, typeof(ScrapModule).Assembly)
    .AddInfrastructure(builder.Configuration)
    .AddFishingModule(builder.Configuration)
    .AddScrapModule();

// Yalnızca geliştirme: SMS sağlayıcısı olmadan telefonda deneme için sabit OTP (üretimde asla etkin olmaz).
if (builder.Environment.IsDevelopment() && builder.Configuration["Development:FixedOtpCode"] is { Length: > 0 } fixedOtp)
{
    builder.Services.AddSingleton<IOtpCodeGenerator>(new FixedOtpCodeGenerator(fixedOtp));
}

// Ters proxy (nginx/ingress) arkasında gerçek istemci IP'si ve şema: rate limiting ve HTTPS yönlendirmesi buna dayanır.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    foreach (var proxy in builder.Configuration.GetSection("Security:KnownProxies").Get<string[]>() ?? [])
    {
        options.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
    }
});

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

// CORS: mobil uygulama CORS'a tabi değildir; yalnızca yapılandırılmış web kökenlerine izin verilir (varsayılan: hiçbiri).
var corsOrigins = builder.Configuration.GetSection("Security:CorsOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(corsOrigins).AllowAnyHeader().WithMethods("GET", "POST", "PUT", "PATCH", "DELETE").AllowCredentials()));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<HttpCurrentUser>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<HttpCurrentUser>());

builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();
builder.Services.AddSingleton<IChatRealtime, SignalRChatRealtime>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.ConfigureOptions<ConfigureJwtBearer>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(JwtTokenClaims.AdminPolicy, policy => policy.RequireClaim(JwtTokenClaims.Role, "Admin"));
    options.AddPolicy(JwtTokenClaims.ModeratorPolicy, policy => policy.RequireClaim(JwtTokenClaims.Role, "Admin", "Moderator"));
});

// OTP uç noktaları için IP bazlı ek koruma (numara bazlı limitler Application katmanında).
var otpPermitPerMinute = builder.Configuration.GetValue("RateLimiting:OtpPermitPerMinute", 10);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthEndpoints.OtpRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = otpPermitPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Sohbet: kullanıcı başına (ek olarak Application katmanında dakikalık mesaj sınırı var).
    options.AddPolicy(MarketEndpoints.ChatRateLimitPolicy, context =>
        RateLimitPartition.GetTokenBucketLimiter(
            context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 10,
                TokensPerPeriod = 1,
                ReplenishmentPeriod = TimeSpan.FromSeconds(3),
                QueueLimit = 0,
            }));
});

builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<ConcurrencyExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    options.AddSecurityDefinition("Bearer", scheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = [] });
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("SahaNet.Api"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            tracing.AddOtlpExporter();
        }
    });

// Gerçek para akışı için sandbox ödeme sağlayıcısı yalnızca geliştirme/test ortamında kabul edilir.
var paymentSandbox = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");
if (!paymentSandbox)
{
    throw new InvalidOperationException(
        "Üretim ortamı için gerçek ödeme sağlayıcısı (iyzico/PayTR) adaptörü yapılandırılmadı; sandbox ile çalıştırılamaz.");
}

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Tüm yanıtlar için temel güvenlik başlıkları.
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var jobOptions = app.Services.GetRequiredService<IOptions<BackgroundJobOptions>>().Value;
if (jobOptions.Enabled && jobOptions.DashboardEnabled)
{
    // Yalnızca sunucunun kendisinden erişilebilir; Faz 7'de yönetici rolüne bağlanacak.
    app.UseHangfireDashboard("/hangfire", new DashboardOptions { Authorization = [new LocalRequestsOnlyAuthorizationFilter()] });
}

// Yönetici paneli (statik, tek sayfa). Katı CSP: yalnızca kendi betikleri, satır içi betik yok.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Headers.ContentSecurityPolicy =
            "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
    }

    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Liveness: süreç ayakta mı (bağımlılık kontrolü yok).
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthResponseWriter.WriteAsync,
});

// Readiness: PostgreSQL ve Redis erişilebilir mi.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(SahaNet.Infrastructure.DependencyInjection.ReadyTag),
    ResponseWriter = HealthResponseWriter.WriteAsync,
});

app.MapCatalogEndpoints();
app.MapAuthEndpoints();
app.MapMeEndpoints();
app.MapAlertEndpoints();
app.MapMarketEndpoints();
app.MapPaymentEndpoints(paymentSandbox);
app.MapAdminEndpoints();
app.MapHub<ChatHub>(ChatHub.Path);
app.MapFishingModule();
app.MapScrapModule();

await app.RunAsync();

/// <summary>WebApplicationFactory ile entegrasyon testleri için görünür kılınır.</summary>
public partial class Program;
