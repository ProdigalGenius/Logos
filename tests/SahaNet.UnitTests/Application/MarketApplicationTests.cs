using SahaNet.Application.Market;
using SahaNet.Domain.Market;
using SahaNet.Modules.Fishing.Application;
using SahaNet.Modules.Fishing.Domain;

namespace SahaNet.UnitTests.Application;

public class MarketApplicationTests
{
    private static ListingCategory FreshFish =>
        new FishingListingCategories().GetCategories().Single(c => c.Key == FishingListingCategories.FreshFish);

    [Fact]
    public void Attributes_ValidAgainstModuleSchema_AreNormalized()
    {
        var result = AttributeValidator.Validate(FreshFish, new Dictionary<string, string>
        {
            ["species"] = "levrek",
            ["port"] = "sariyer",
            ["pickupAt"] = "2026-10-17T07:00:00+03:00",
        });

        result.IsSuccess.Should().BeTrue();
        result.Value["pickupAt"].Should().Be("2026-10-17T04:00:00.0000000+00:00");
    }

    [Theory]
    [InlineData("species", "balina", "Tür")]
    [InlineData("port", "", "zorunlu")]
    [InlineData("pickupAt", "yarın sabah", "tarih")]
    [InlineData("color", "kırmızı", "Bilinmeyen")]
    public void Attributes_Invalid_AreRejected(string key, string value, string expectedText)
    {
        var input = new Dictionary<string, string>
        {
            ["species"] = "levrek",
            ["port"] = "sariyer",
            ["pickupAt"] = "2026-10-17T07:00:00+03:00",
            [key] = value,
        };

        var result = AttributeValidator.Validate(FreshFish, input);

        result.Error.Code.Should().Be("market.attribute_invalid");
        result.Error.Message.Should().Contain(expectedText);
    }

    [Fact]
    public void Number_AcceptsTurkishDecimalComma()
    {
        var equipment = new FishingListingCategories().GetCategories().Single(c => c.Key == FishingListingCategories.Equipment);

        var result = AttributeValidator.Validate(equipment, new Dictionary<string, string>
        {
            ["kind"] = "tekne",
            ["condition"] = "kullanilmis",
            ["lengthM"] = "7,5",
        });

        result.Value["lengthM"].Should().Be("7.5");
    }

    [Fact]
    public void WhatsAppLink_ContainsTitlePriceAndDeepLink()
    {
        var listing = Listing.Create(Guid.NewGuid(), "fishing", "fresh-fish", "Taze levrek", null, ListingType.FixedPrice, 1250.5m, 10, "kg", null,
            new Dictionary<string, string>(), DateTimeOffset.UtcNow.AddDays(1), null, null, DateTimeOffset.UtcNow);

        var link = Uri.UnescapeDataString(WhatsAppShare.Link(listing, "https://balikcinet.app/"));

        link.Should().StartWith("https://wa.me/?text=").And.Contain("Taze levrek").And.Contain("1.250,50 TL/kg")
            .And.Contain($"https://balikcinet.app/l/{listing.Id:N}");
    }

    [Fact]
    public void PriceMedian_IgnoresOutliers()
    {
        var user = Guid.NewGuid();
        var date = new DateOnly(2026, 10, 16);
        var reports = new[] { 400m, 420m, 410m, 5000m }
            .Select(p => MarketPrice.Report("levrek", "34", date, p, user, DateTimeOffset.UtcNow));

        var daily = PriceAggregation.Daily(reports, new Dictionary<string, string> { ["levrek"] = "Levrek" });

        daily.Should().ContainSingle();
        daily[0].Median.Should().Be(415m);
        daily[0].Max.Should().Be(5000m);
        daily[0].ProvinceName.Should().Be("İstanbul");
    }
}
