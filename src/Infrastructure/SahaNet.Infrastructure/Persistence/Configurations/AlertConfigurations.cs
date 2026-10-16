using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite.Geometries;
using SahaNet.Domain.Alerts;
using SahaNet.Domain.Geo;
using SahaNet.Domain.Identity;
using SahaNet.Domain.Notifications;

namespace SahaNet.Infrastructure.Persistence.Configurations;

internal static class GeoConversions
{
    public const int Wgs84 = 4326;

    /// <summary>Domain GeoPoint ↔ PostGIS geography(Point, 4326).</summary>
    public static readonly ValueConverter<GeoPoint, Point> PointConverter = new(
        g => new Point(g.Longitude, g.Latitude) { SRID = Wgs84 },
        p => GeoPoint.FromTrusted(p.Y, p.X));
}

internal sealed class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    public const string Table = "regions";

    public void Configure(EntityTypeBuilder<Region> builder)
    {
        // Ham PostGIS sorguları için kolon adları sabit (snake_case).
        builder.ToTable(Table, SahaNetDbContext.AlertsSchema);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
        builder.Property(r => r.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(16);
        builder.Property(r => r.Center).HasColumnName("center")
            .HasConversion(GeoConversions.PointConverter)
            .HasColumnType("geography (point, 4326)")
            .IsRequired();
        builder.Property(r => r.RadiusKm).HasColumnName("radius_km");
        builder.Property(r => r.ProvinceCode).HasColumnName("province_code").HasMaxLength(2);
        builder.Property(r => r.IsActive).HasColumnName("is_active");

        builder.HasIndex(r => r.Code).IsUnique();
        builder.HasIndex(r => r.Center).HasMethod("gist");

        builder.HasData(RegionSeed.All);
    }
}

internal sealed class RegionSubscriptionConfiguration : IEntityTypeConfiguration<RegionSubscription>
{
    public void Configure(EntityTypeBuilder<RegionSubscription> builder)
    {
        builder.ToTable("region_subscriptions", SahaNetDbContext.AlertsSchema);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Topics).HasConversion<int>();
        builder.HasIndex(s => new { s.UserId, s.RegionId }).IsUnique();
        builder.HasIndex(s => s.RegionId);

        builder.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Region>().WithMany().HasForeignKey(s => s.RegionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DeviceRegistrationConfiguration : IEntityTypeConfiguration<DeviceRegistration>
{
    public void Configure(EntityTypeBuilder<DeviceRegistration> builder)
    {
        builder.ToTable("devices", SahaNetDbContext.AlertsSchema);
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Token).HasMaxLength(DeviceRegistration.TokenMaxLength).IsRequired();
        builder.Property(d => d.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(d => d.Platform).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(d => d.TokenHash).IsUnique();
        builder.HasIndex(d => d.UserId);

        builder.HasOne<User>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("alerts", SahaNetDbContext.AlertsSchema);
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(a => a.Severity).HasConversion<string>().HasMaxLength(16);
        builder.Property(a => a.Title).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Message).HasMaxLength(1000).IsRequired();
        builder.Property(a => a.DedupKey).HasMaxLength(128).IsRequired();
        builder.HasIndex(a => a.DedupKey).IsUnique();
        builder.HasIndex(a => new { a.RegionId, a.ValidTo });
        builder.Ignore(a => a.DomainEvents);

        builder.HasOne<Region>().WithMany().HasForeignKey(a => a.RegionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AlertDeliveryConfiguration : IEntityTypeConfiguration<AlertDelivery>
{
    public void Configure(EntityTypeBuilder<AlertDelivery> builder)
    {
        builder.ToTable("alert_deliveries", SahaNetDbContext.AlertsSchema);
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Channel).HasConversion<string>().HasMaxLength(16);
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(d => new { d.AlertId, d.UserId });
        builder.HasIndex(d => d.CreatedAt);

        builder.HasOne<Alert>().WithMany().HasForeignKey(d => d.AlertId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
