namespace SahaNet.Application.Common;

public sealed record Region(string Code, string Name);

/// <summary>Bölge seçimi için Türkiye illeri (plaka kodu). Faz 2'de PostGIS bölgeleriyle zenginleşecek.</summary>
public static class RegionCatalog
{
    private static readonly string[] ProvinceNames =
    [
        "Adana", "Adıyaman", "Afyonkarahisar", "Ağrı", "Amasya", "Ankara", "Antalya", "Artvin", "Aydın", "Balıkesir",
        "Bilecik", "Bingöl", "Bitlis", "Bolu", "Burdur", "Bursa", "Çanakkale", "Çankırı", "Çorum", "Denizli",
        "Diyarbakır", "Edirne", "Elazığ", "Erzincan", "Erzurum", "Eskişehir", "Gaziantep", "Giresun", "Gümüşhane", "Hakkâri",
        "Hatay", "Isparta", "Mersin", "İstanbul", "İzmir", "Kars", "Kastamonu", "Kayseri", "Kırklareli", "Kırşehir",
        "Kocaeli", "Konya", "Kütahya", "Malatya", "Manisa", "Kahramanmaraş", "Mardin", "Muğla", "Muş", "Nevşehir",
        "Niğde", "Ordu", "Rize", "Sakarya", "Samsun", "Siirt", "Sinop", "Sivas", "Tekirdağ", "Tokat",
        "Trabzon", "Tunceli", "Şanlıurfa", "Uşak", "Van", "Yozgat", "Zonguldak", "Aksaray", "Bayburt", "Karaman",
        "Kırıkkale", "Batman", "Şırnak", "Bartın", "Ardahan", "Iğdır", "Yalova", "Karabük", "Kilis", "Osmaniye",
        "Düzce",
    ];

    public static IReadOnlyList<Region> All { get; } = ProvinceNames
        .Select((name, index) => new Region((index + 1).ToString("D2", System.Globalization.CultureInfo.InvariantCulture), name))
        .ToList();

    private static readonly Dictionary<string, Region> ByCode = All.ToDictionary(r => r.Code, StringComparer.Ordinal);

    public static Region? Find(string? code) =>
        code is not null && ByCode.TryGetValue(code, out var region) ? region : null;
}
