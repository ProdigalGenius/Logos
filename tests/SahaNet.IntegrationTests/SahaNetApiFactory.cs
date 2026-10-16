using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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
        builder.UseSetting("Redis:ConnectionString", _redis.GetConnectionString());
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<SahaNetApiFactory>
{
    public const string Name = "api";
}
