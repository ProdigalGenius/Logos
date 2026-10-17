using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Application.Modules;
using SahaNet.Modules.Scrap.Domain;

namespace SahaNet.Modules.Scrap.Persistence;

internal sealed class ScrapDbContext(DbContextOptions<ScrapDbContext> options, IPersonalDataProtector protector) : DbContext(options)
{
    public const string Schema = "scrap";

    public DbSet<Material> Materials => Set<Material>();

    public DbSet<ScrapPriceReport> Prices => Set<ScrapPriceReport>();

    public DbSet<PickupRequest> Pickups => Set<PickupRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Material>(b =>
        {
            b.ToTable("materials");
            b.HasKey(m => m.Code);
            b.Property(m => m.Code).HasMaxLength(32);
            b.Property(m => m.Name).HasMaxLength(64).IsRequired();
            b.Property(m => m.Group).HasMaxLength(32).IsRequired();
            b.HasData(ScrapSeed.Materials);
        });

        modelBuilder.Entity<ScrapPriceReport>(b =>
        {
            b.ToTable("price_reports");
            b.HasKey(p => p.Id);
            b.Property(p => p.Id).ValueGeneratedNever();
            b.Property(p => p.MaterialCode).HasMaxLength(32).IsRequired();
            b.Property(p => p.ProvinceCode).HasMaxLength(2).IsRequired();
            b.Property(p => p.PricePerKg).HasPrecision(12, 2);
            b.HasIndex(p => new { p.Date, p.MaterialCode, p.ProvinceCode });
            b.HasIndex(p => new { p.ReportedBy, p.Date, p.MaterialCode, p.ProvinceCode }).IsUnique();
        });

        modelBuilder.Entity<PickupRequest>(b =>
        {
            b.ToTable("pickups");
            b.HasKey(p => p.Id);
            b.Property(p => p.Id).ValueGeneratedNever();
            b.Property(p => p.ProvinceCode).HasMaxLength(2).IsRequired();
            b.Property(p => p.District).HasMaxLength(64).IsRequired();
            b.Property(p => p.Address)
                .HasColumnName("address_encrypted")
                .HasConversion(a => protector.Protect(a), s => protector.Unprotect(s))
                .HasMaxLength(1024)
                .IsRequired();
            b.Property(p => p.MaterialCodes).HasMaxLength(256).IsRequired();
            b.Property(p => p.EstimatedKg).HasPrecision(10, 1);
            b.Property(p => p.Notes).HasMaxLength(PickupRequest.NotesMaxLength);
            b.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
            b.Property<uint>("RowVersion").IsRowVersion();
            b.Ignore(p => p.Materials);
            b.HasIndex(p => new { p.Status, p.ProvinceCode, p.CreatedAt });
            b.HasIndex(p => p.RequesterId);
            b.HasIndex(p => p.CollectorId);
        });
    }
}

internal static class ScrapSeed
{
    public static Material[] Materials =>
    [
        new("demir", "Demir / çelik", "metal"),
        new("bakir", "Bakır", "metal"),
        new("aluminyum", "Alüminyum", "metal"),
        new("sari", "Sarı (pirinç)", "metal"),
        new("krom", "Krom / paslanmaz", "metal"),
        new("aku", "Akü", "tehlikeli"),
        new("kablo", "Kablo", "metal"),
        new("karton", "Kağıt / karton", "kağıt"),
        new("plastik", "Plastik", "plastik"),
        new("elektronik", "Elektronik atık", "elektronik"),
        new("beyaz-esya", "Beyaz eşya", "elektronik"),
    ];
}

/// <summary>Şemayı hazırlar (balıkçı modülüyle aynı desen; çekirdek başlatıcısı çağırır).</summary>
internal sealed class ScrapSchemaInitializer(ScrapDbContext db) : IModuleSchemaInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (db.Database.GetMigrations().Any())
        {
            await db.Database.MigrateAsync(cancellationToken);
            return;
        }

        var exists = await db.Database
            .SqlQuery<bool>($"SELECT to_regclass('scrap.materials') IS NOT NULL AS \"Value\"")
            .SingleAsync(cancellationToken);
        if (!exists)
        {
            await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync(cancellationToken);
        }
    }
}
