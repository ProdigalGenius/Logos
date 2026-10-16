using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Alerts;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Application.Retention;
using SahaNet.Application.Weather;
using SahaNet.Infrastructure.Jobs;
using SahaNet.Infrastructure.Options;
using SahaNet.Infrastructure.Persistence;
using SahaNet.Infrastructure.Persistence.Repositories;
using SahaNet.Infrastructure.Push;
using SahaNet.Infrastructure.Security;
using SahaNet.Infrastructure.Sms;
using SahaNet.Infrastructure.Weather;
using StackExchange.Redis;

namespace SahaNet.Infrastructure;

public static class DependencyInjection
{
    public const string ReadyTag = "ready";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        AddValidatedOptions<DatabaseOptions>(services, DatabaseOptions.SectionName);
        AddValidatedOptions<RedisOptions>(services, RedisOptions.SectionName);
        AddValidatedOptions<PersonalDataOptions>(services, PersonalDataOptions.SectionName);
        AddValidatedOptions<JwtOptions>(services, JwtOptions.SectionName);
        AddValidatedOptions<WeatherOptions>(services, WeatherOptions.SectionName);
        AddValidatedOptions<PushOptions>(services, PushOptions.SectionName);
        AddValidatedOptions<BackgroundJobOptions>(services, BackgroundJobOptions.SectionName);
        services.AddOptions<SmsOptions>().BindConfiguration(SmsOptions.SectionName);

        AddPersistence(services);
        AddSecurity(services);
        AddRedis(services);
        AddMessaging(services, configuration);
        AddWeather(services);
        AddBackgroundJobs(services, configuration);

        services.AddHealthChecks()
            .AddNpgSql(sp => sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString, name: "postgresql", tags: [ReadyTag])
            .AddRedis(sp => sp.GetRequiredService<IConnectionMultiplexer>(), name: "redis", tags: [ReadyTag]);

        return services;
    }

    private static void AddPersistence(IServiceCollection services)
    {
        services.AddDbContext<SahaNetDbContext>((sp, options) =>
        {
            var db = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(db.ConnectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.EnableRetryOnFailure(db.MaxRetryCount);
                npgsql.MigrationsHistoryTable("__ef_migrations_history", SahaNetDbContext.CoreSchema);
            });
        });
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<SahaNetDbContext>());
        services.AddHostedService<DatabaseInitializer>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOtpChallengeRepository, OtpChallengeRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IConsentRepository, ConsentRepository>();
        services.AddScoped<IRegionRepository, RegionRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IDataRetentionStore, DataRetentionStore>();
    }

    private static void AddSecurity(IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IPersonalDataProtector, AesGcmPersonalDataProtector>();
        services.AddSingleton<IOtpCodeGenerator, OtpCodeGenerator>();
        services.AddSingleton<ITokenService, JwtTokenService>();
    }

    private static void AddRedis(IServiceCollection services)
    {
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var redis = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
            var config = ConfigurationOptions.Parse(redis.ConnectionString);
            config.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(config);
        });

        services.AddStackExchangeRedisCache(_ => { });
        services.AddOptions<Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions>()
            .Configure<IServiceProvider>((cache, sp) =>
            {
                cache.ConnectionMultiplexerFactory = () => Task.FromResult(sp.GetRequiredService<IConnectionMultiplexer>());
                cache.InstanceName = "sahanet:";
            });
    }

    private static void AddMessaging(IServiceCollection services, IConfiguration configuration)
    {
        var sms = configuration.GetSection(SmsOptions.SectionName).Get<SmsOptions>() ?? new SmsOptions();
        if (sms.Provider == SmsProvider.Netgsm)
        {
            services.AddHttpClient<ISmsSender, NetgsmSmsSender>().AddStandardResilienceHandler();
        }
        else
        {
            services.AddSingleton<ISmsSender, LoggingSmsSender>();
        }

        var push = configuration.GetSection(PushOptions.SectionName).Get<PushOptions>() ?? new PushOptions();
        if (push.Provider == PushProvider.Fcm)
        {
            services.AddSingleton<IPushSender, FcmPushSender>();
        }
        else
        {
            services.AddSingleton<IPushSender, LoggingPushSender>();
        }
    }

    private static void AddWeather(IServiceCollection services)
    {
        services.AddHttpClient<OpenMeteoWeatherProvider>(http => http.Timeout = TimeSpan.FromSeconds(20))
            .AddStandardResilienceHandler();
        services.AddScoped<IWeatherProvider, CachedWeatherProvider>();
    }

    private static void AddBackgroundJobs(IServiceCollection services, IConfiguration configuration)
    {
        var jobs = configuration.GetSection(BackgroundJobOptions.SectionName).Get<BackgroundJobOptions>() ?? new BackgroundJobOptions();
        services.AddScoped<PlatformJobs>();

        if (!jobs.Enabled)
        {
            services.AddScoped<IAlertDispatchScheduler, InlineAlertDispatchScheduler>();
            return;
        }

        services.AddHangfire((sp, config) => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(storage => storage.UseNpgsqlConnection(
                sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString)));
        services.AddHangfireServer();
        services.AddHostedService<RecurringJobRegistrar>();
        services.AddScoped<IAlertDispatchScheduler, HangfireAlertDispatchScheduler>();
    }

    private static void AddValidatedOptions<T>(IServiceCollection services, string section)
        where T : class =>
        services.AddOptions<T>()
            .BindConfiguration(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
}
