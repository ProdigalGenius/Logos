using SahaNet.Domain.Common;

namespace SahaNet.Domain.Geo;

public enum RegionKind
{
    SeaArea = 0,
    Port = 1,
    Land = 2,
}

/// <summary>
/// Uyarı ve abonelik birimi olan coğrafi bölge (deniz alanı, liman ...). MVP'de merkez + yarıçap
/// ile tanımlanır; PostGIS sorguları merkez noktası (geography) üzerinden yapılır.
/// </summary>
public sealed class Region : Entity<Guid>
{
    public Region(Guid id, string code, string name, RegionKind kind, GeoPoint center, double radiusKm, string? provinceCode)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(center);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radiusKm);

        Code = code;
        Name = name;
        Kind = kind;
        Center = center;
        RadiusKm = radiusKm;
        ProvinceCode = provinceCode;
        IsActive = true;
    }

    // EF Core
    private Region()
        : base(Guid.Empty)
    {
        Code = null!;
        Name = null!;
        Center = null!;
    }

    /// <summary>Sabit, URL'de kullanılabilir anahtar (örn. "marmara").</summary>
    public string Code { get; private set; }

    public string Name { get; private set; }

    public RegionKind Kind { get; private set; }

    public GeoPoint Center { get; private set; }

    public double RadiusKm { get; private set; }

    /// <summary>Bağlı olduğu il (plaka kodu), deniz alanları için boş olabilir.</summary>
    public string? ProvinceCode { get; private set; }

    public bool IsActive { get; private set; }

    public bool Contains(GeoPoint point) => Center.DistanceKmTo(point) <= RadiusKm;
}
