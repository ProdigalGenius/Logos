using SahaNet.Domain.Geo;
using SahaNet.Modules.Fishing.Domain;

namespace SahaNet.Modules.Fishing.Persistence;

/// <summary>
/// Başlangıç verisi. UYARI: Asgari boylar ve yasak dönemleri TASLAKTIR; yayından önce yürürlükteki
/// "Ticari Amaçlı Su Ürünleri Avcılığının Düzenlenmesi Hakkında Tebliğ" ile doğrulanmalıdır.
/// Faz 7'de yönetici paneliyle güncellenecek.
/// </summary>
internal static class FishingSeed
{
    public static Port[] Ports =>
    [
        Port(1, "rumelifeneri", "Rumelifeneri", "34", 41.2340, 29.1110),
        Port(2, "sariyer", "Sarıyer", "34", 41.1690, 29.0570),
        Port(3, "kumkapi", "Kumkapı", "34", 41.0010, 28.9650),
        Port(4, "tuzla", "Tuzla", "34", 40.8150, 29.2990),
        Port(5, "sile", "Şile", "34", 41.1790, 29.6110),
        Port(6, "tekirdag", "Tekirdağ", "59", 40.9750, 27.5150),
        Port(7, "erdek", "Erdek", "10", 40.3970, 27.7930),
        Port(8, "canakkale", "Çanakkale", "17", 40.1530, 26.4050),
        Port(9, "ayvalik", "Ayvalık", "10", 39.3190, 26.6930),
        Port(10, "foca", "Foça", "35", 38.6700, 26.7540),
        Port(11, "cesme", "Çeşme", "35", 38.3240, 26.3030),
        Port(12, "bodrum", "Bodrum", "48", 37.0340, 27.4240),
        Port(13, "fethiye", "Fethiye", "48", 36.6230, 29.1030),
        Port(14, "antalya", "Antalya", "07", 36.8840, 30.7040),
        Port(15, "mersin", "Mersin", "33", 36.7950, 34.6210),
        Port(16, "iskenderun", "İskenderun", "31", 36.5920, 36.1760),
        Port(17, "eregli", "Karadeniz Ereğli", "67", 41.2830, 31.4180),
        Port(18, "amasra", "Amasra", "74", 41.7480, 32.3870),
        Port(19, "sinop", "Sinop", "57", 42.0230, 35.1530),
        Port(20, "samsun", "Samsun", "55", 41.2930, 36.3400),
        Port(21, "trabzon", "Trabzon", "61", 41.0050, 39.7360),
        Port(22, "rize", "Rize", "53", 41.0280, 40.5200),
    ];

    public static Species[] Species =>
    [
        new("levrek", "Levrek", "Dicentrarchus labrax", 25),
        new("cipura", "Çipura", "Sparus aurata", 20),
        new("barbunya", "Barbunya", "Mullus barbatus", 13),
        new("tekir", "Tekir", "Mullus surmuletus", 13),
        new("mezgit", "Mezgit", "Merlangius merlangus", 13),
        new("hamsi", "Hamsi", "Engraulis encrasicolus", 9),
        new("istavrit", "İstavrit", "Trachurus mediterraneus", 13),
        new("lufer", "Lüfer", "Pomatomus saltatrix", 20),
        new("palamut", "Palamut", "Sarda sarda", 25),
        new("kalkan", "Kalkan", "Scophthalmus maximus", 45),
        new("sardalya", "Sardalya", "Sardina pilchardus", 11),
        new("uskumru", "Uskumru", "Scomber scombrus", 18),
        new("kolyoz", "Kolyoz", "Scomber colias", 18),
        new("kefal", "Kefal", "Mugil cephalus", 20),
        new("mercan", "Mercan", "Pagellus erythrinus", 15),
        new("sinarit", "Sinarit", "Dentex dentex", 20),
        new("orfoz", "Orfoz", "Epinephelus marginatus", 45),
        new("lagos", "Lagos", "Epinephelus aeneus", 45),
        new("dil", "Dil", "Solea solea", 20),
        new("kilic", "Kılıç", "Xiphias gladius", 125),
        new("istakoz", "Istakoz", "Homarus gammarus", 25),
        new("kalamar", "Kalamar", "Loligo vulgaris", null),
        new("ahtapot", "Ahtapot", "Octopus vulgaris", null),
        new("diger", "Diğer", null, null),
    ];

    public static FishingBan[] Bans =>
    [
        new(BanId(1), "Genel av yasağı (gırgır ve trol)", 4, 15, 8, 31, null, null, "Gırgır, trol",
            "TASLAK — Denizlerde gırgır ve trol ile avcılık yasak. Amatör avcılık kuralları ayrıdır."),
        new(BanId(2), "Kalkan av yasağı (Karadeniz)", 4, 15, 6, 15, "kalkan", null, null,
            "TASLAK — Karadeniz'de kalkan avı yasak dönem."),
        new(BanId(3), "Orfoz ve lagos koruma", 1, 1, 12, 31, "orfoz", null, "Zıpkın (tüplü)",
            "TASLAK — Tüplü dalışla zıpkın avı her zaman yasak."),
        new(BanId(4), "Lüfer küçük boy (çinekop) koruma", 5, 1, 8, 31, "lufer", null, null,
            "TASLAK — Dönem ve kapsam tebliğe göre doğrulanmalı."),
    ];

    private static Port Port(int n, string code, string name, string province, double lat, double lon) =>
        new(new Guid($"f15e0000-0000-4000-8000-{n:D12}"), code, name, province, GeoPoint.FromTrusted(lat, lon));

    private static Guid BanId(int n) => new($"f15eba40-0000-4000-8000-{n:D12}");
}
