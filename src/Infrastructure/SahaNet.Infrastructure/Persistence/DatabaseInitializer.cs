using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SahaNet.Application.Modules;
using SahaNet.Infrastructure.Options;

namespace SahaNet.Infrastructure.Persistence;

/// <summary>Açılışta şemayı hazırlar (yalnızca <see cref="DatabaseOptions.ApplyMigrationsOnStartup"/> açıksa).</summary>
internal sealed class DatabaseInitializer(IServiceScopeFactory scopeFactory, IOptions<DatabaseOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.ApplyMigrationsOnStartup)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SahaNetDbContext>();

        if (db.Database.GetMigrations().Any())
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }

        // Modüller kendi şemalarını çekirdek şemadan sonra hazırlar (Users tablosuna bağımlı olabilirler).
        foreach (var module in scope.ServiceProvider.GetServices<IModuleSchemaInitializer>())
        {
            await module.InitializeAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
