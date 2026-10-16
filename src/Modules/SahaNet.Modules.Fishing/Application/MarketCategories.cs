using SahaNet.Application.Market;
using SahaNet.Domain.Market;
using SahaNet.Modules.Fishing.Persistence;

namespace SahaNet.Modules.Fishing.Application;

/// <summary>Balıkçı nişinin pazar kategorileri; çekirdek ilan modülünü şemayla genişletir.</summary>
internal sealed class FishingListingCategories : IListingCategoryProvider
{
    public const string FreshFish = "fresh-fish";
    public const string Equipment = "fishing-equipment";

    private static readonly IReadOnlyList<string> SpeciesCodes = FishingSeed.Species.Select(s => s.Code).ToList();
    private static readonly IReadOnlyList<string> PortCodes = FishingSeed.Ports.Select(p => p.Code).ToList();

    private static readonly ListingCategory[] Categories =
    [
        // "Yarın 07:00'de X limanında 20 kg levrek": sabit fiyatla ön sipariş/rezervasyon veya ihale.
        new(
            FreshFish,
            FishingModuleDescriptor.ModuleKey,
            "Taze balık",
            "kg",
            [ListingType.FixedPrice, ListingType.Auction],
            [
                new AttributeDefinition("species", "Tür", AttributeType.Choice, Required: true, Choices: SpeciesCodes),
                new AttributeDefinition("port", "Teslim limanı", AttributeType.Choice, Required: true, Choices: PortCodes),
                new AttributeDefinition("pickupAt", "Teslim zamanı", AttributeType.DateTime, Required: true),
                new AttributeDefinition("caughtWith", "Av aracı", AttributeType.Choice, Required: false, Choices: ["olta", "paragat", "uzatma-ag", "sepet", "diger"]),
            ]),
        new(
            Equipment,
            FishingModuleDescriptor.ModuleKey,
            "Ekipman (ağ, motor, tekne)",
            "adet",
            [ListingType.FixedPrice, ListingType.OfferBased, ListingType.Auction],
            [
                new AttributeDefinition("kind", "Ekipman türü", AttributeType.Choice, Required: true, Choices: ["ag", "motor", "tekne", "olta-takimi", "elektronik", "diger"]),
                new AttributeDefinition("condition", "Durum", AttributeType.Choice, Required: true, Choices: ["sifir", "az-kullanilmis", "kullanilmis", "tamir-gerekli"]),
                new AttributeDefinition("brand", "Marka", AttributeType.Text, Required: false, MaxLength: 60),
                new AttributeDefinition("year", "Yıl", AttributeType.Number, Required: false),
                new AttributeDefinition("lengthM", "Boy (m)", AttributeType.Number, Required: false),
                new AttributeDefinition("horsepower", "Beygir gücü", AttributeType.Number, Required: false),
            ]),
    ];

    public IEnumerable<ListingCategory> GetCategories() => Categories;
}
