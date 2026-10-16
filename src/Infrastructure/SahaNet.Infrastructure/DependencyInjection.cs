using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Infrastructure.Options;
using SahaNet.Infrastructure.Persistence;
using SahaNet.Infrastructure.Persistence.Repositories;
using SahaNet.Infrastructure.Security;
using SahaNet.Infrastructure.Sms;
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
        services.AddOptions<SmsOptions>().BindConfiguration(SmsOptions.SectionName);

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

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IPersonalDataProtector, AesGcmPersonalDataProtector>();
        services.AddSingleton<IOtpCodeGenerator, OtpCodeGenerator>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<ISmsSender, LoggingSmsSender>();

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var redis = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
            var config = ConfigurationOptions.Parse(redis.ConnectionString);
            config.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(config);
        });

        services.AddHealthChecks()
            .AddNpgSql(sp => sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString, name: "postgresql", tags: [ReadyTag])
            .AddRedis(sp => sp.GetRequiredService<IConnectionMultiplexer>(), name: "redis", tags: [ReadyTag]);

        return services;
    }

    private static void AddValidatedOptions<T>(IServiceCollection services, string section)
        where T : class =>
        services.AddOptions<T>()
            .BindConfiguration(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
}
