using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using SahaNet.Application.Modules;

namespace SahaNet.Modules.Fishing.Persistence;

/// <summary>
/// Modül şemasını hazırlar. Migration varsa uygular; yoksa (geliştirme) "fishing" tabloları
/// henüz yoksa oluşturur. EnsureCreated paylaşılan veritabanında çalışmadığı için kullanılmaz.
/// </summary>
internal sealed class FishingSchemaInitializer(FishingDbContext db) : IModuleSchemaInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (db.Database.GetMigrations().Any())
        {
            await db.Database.MigrateAsync(cancellationToken);
            return;
        }

        var exists = await db.Database
            .SqlQuery<bool>($"SELECT to_regclass('fishing.ports') IS NOT NULL AS \"Value\"")
            .SingleAsync(cancellationToken);
        if (!exists)
        {
            await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync(cancellationToken);
        }
    }
}
