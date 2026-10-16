using SahaNet.Domain.Geo;

namespace SahaNet.Infrastructure.Persistence.Configurations;

/// <summary>
/// Türkiye kıyı deniz alanları (MVP). Merkez + yarıçap yaklaşık değerlerdir; tahmin noktası olarak
/// denizdeki merkez kullanılır. Faz 3'te liman bazlı bölgeler eklenecek.
/// </summary>
internal static class RegionSeed
{
    public static readonly Guid IstanbulStrait = new("5e0c1a00-0000-4000-8000-000000000001");

    public static Region[] All =>
    [
        Sea(IstanbulStrait, "istanbul-bogazi", "İstanbul Boğazı", 41.12, 29.06, 25, "34"),
        Sea(Id(2), "marmara-dogu", "Doğu Marmara", 40.75, 29.00, 45, "34"),
        Sea(Id(3), "marmara-bati", "Batı Marmara", 40.65, 27.80, 55, "59"),
        Sea(Id(4), "canakkale-bogazi", "Çanakkale Boğazı", 40.20, 26.40, 35, "17"),
        Sea(Id(5), "karadeniz-bati", "Batı Karadeniz", 41.60, 30.50, 120, null),
        Sea(Id(6), "karadeniz-orta", "Orta Karadeniz", 42.00, 34.50, 150, null),
        Sea(Id(7), "karadeniz-dogu", "Doğu Karadeniz", 41.30, 39.50, 150, null),
        Sea(Id(8), "ege-kuzey", "Kuzey Ege", 39.60, 26.00, 100, null),
        Sea(Id(9), "ege-orta", "Orta Ege", 38.50, 26.40, 90, null),
        Sea(Id(10), "ege-guney", "Güney Ege", 37.20, 27.00, 90, null),
        Sea(Id(11), "akdeniz-bati", "Batı Akdeniz", 36.30, 29.50, 120, null),
        Sea(Id(12), "akdeniz-orta", "Orta Akdeniz", 36.00, 32.50, 140, null),
        Sea(Id(13), "akdeniz-dogu", "Doğu Akdeniz", 36.50, 35.20, 120, null),
    ];

    private static Guid Id(int n) => new($"5e0c1a00-0000-4000-8000-{n:D12}");

    private static Region Sea(Guid id, string code, string name, double lat, double lon, double radiusKm, string? province) =>
        new(id, code, name, RegionKind.SeaArea, GeoPoint.FromTrusted(lat, lon), radiusKm, province);
}
