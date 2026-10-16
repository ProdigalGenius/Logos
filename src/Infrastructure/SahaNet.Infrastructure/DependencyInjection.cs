using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SahaNet.Application.Abstractions;
using SahaNet.Infrastructure.Options;
using SahaNet.Infrastructure.Persistence;
using StackExchange.Redis;

namespace SahaNet.Infrastructure;

public static class DependencyInjection
{
    public const string ReadyTag = "ready";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

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
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

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
}
