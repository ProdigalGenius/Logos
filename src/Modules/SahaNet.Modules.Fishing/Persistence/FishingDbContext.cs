using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite.Geometries;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Domain.Geo;
using SahaNet.Modules.Fishing.Domain;

namespace SahaNet.Modules.Fishing.Persistence;

/// <summary>Balıkçı modülünün kendi bağlamı ve "fishing" şeması. Çekirdek tablolarına FK vermez.</summary>
internal sealed class FishingDbContext(DbContextOptions<FishingDbContext> options, IPersonalDataProtector protector) : DbContext(options)
{
    public const string Schema = "fishing";

    public DbSet<Port> Ports => Set<Port>();

    public DbSet<Species> Species => Set<Species>();

    public DbSet<FishingBan> Bans => Set<FishingBan>();

    public DbSet<CatchSpot> Spots => Set<CatchSpot>();

    public DbSet<CatchEntry> Catches => Set<CatchEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        var pointConverter = new ValueConverter<GeoPoint, Point>(
            g => new Point(g.Longitude, g.Latitude) { SRID = 4326 },
            p => GeoPoint.FromTrusted(p.Y, p.X));

        // KVKK + ticari sır: av noktası koordinatları şifreli metin olarak saklanır.
        var encryptedPointConverter = new ValueConverter<GeoPoint, string>(
            g => protector.Protect(FormattableString.Invariant($"{g.Latitude:R};{g.Longitude:R}")),
            s => ParsePoint(protector.Unprotect(s)));

        modelBuilder.Entity<Port>(b =>
        {
            b.ToTable("ports");
            b.HasKey(p => p.Id);
            b.Property(p => p.Id).ValueGeneratedNever();
            b.Property(p => p.Code).HasMaxLength(64).IsRequired();
            b.Property(p => p.Name).HasMaxLength(128).IsRequired();
            b.Property(p => p.ProvinceCode).HasMaxLength(2).IsRequired();
            b.Property(p => p.Location).HasConversion(pointConverter).HasColumnType("geography (point, 4326)").IsRequired();
            b.HasIndex(p => p.Code).IsUnique();
            b.HasData(FishingSeed.Ports);
        });

        modelBuilder.Entity<Species>(b =>
        {
            b.ToTable("species");
            b.HasKey(s => s.Code);
            b.Property(s => s.Code).HasMaxLength(32);
            b.Property(s => s.Name).HasMaxLength(64).IsRequired();
            b.Property(s => s.ScientificName).HasMaxLength(128);
            b.HasData(FishingSeed.Species);
        });

        modelBuilder.Entity<FishingBan>(b =>
        {
            b.ToTable("bans");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.SpeciesCode).HasMaxLength(32);
            b.Property(x => x.RegionCode).HasMaxLength(64);
            b.Property(x => x.Gear).HasMaxLength(64);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.HasData(FishingSeed.Bans);
        });

        modelBuilder.Entity<CatchSpot>(b =>
        {
            b.ToTable("spots");
            b.HasKey(s => s.Id);
            b.Property(s => s.Id).ValueGeneratedNever();
            b.Property(s => s.Name).HasMaxLength(CatchSpot.NameMaxLength).IsRequired();
            b.Property(s => s.Location).HasColumnName("location_encrypted").HasConversion(encryptedPointConverter).HasMaxLength(256).IsRequired();
            b.Property(s => s.Notes).HasMaxLength(CatchSpot.NotesMaxLength);
            b.Property(s => s.SpeciesCodes).HasMaxLength(512);
            b.HasIndex(s => s.OwnerId);
            b.Ignore(s => s.SpeciesList);

            b.OwnsMany(s => s.Shares, share =>
            {
                share.ToTable("spot_shares");
                share.WithOwner().HasForeignKey(x => x.SpotId);

                // Üretilen anahtar: sahip yüklüyken eklenen paylaşım EF tarafından "yeni" olarak algılanır.
                // (Doğal anahtar dolu gelse EF onu mevcut kayıt sanıp UPDATE dener.)
                share.Property<long>("Id").UseIdentityAlwaysColumn();
                share.HasKey("Id");
                share.HasIndex(x => new { x.SpotId, x.UserId }).IsUnique();
                share.HasIndex(x => x.UserId);
            });
        });

        modelBuilder.Entity<CatchEntry>(b =>
        {
            b.ToTable("catches");
            b.HasKey(c => c.Id);
            b.Property(c => c.Id).ValueGeneratedNever();
            b.Property(c => c.SpeciesCode).HasMaxLength(32).IsRequired();
            b.Property(c => c.Notes).HasMaxLength(CatchEntry.NotesMaxLength);
            b.HasIndex(c => new { c.UserId, c.CaughtAt });
        });
    }

    private static GeoPoint ParsePoint(string value)
    {
        var parts = value.Split(';');
        return GeoPoint.FromTrusted(
            double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
            double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
    }
}
