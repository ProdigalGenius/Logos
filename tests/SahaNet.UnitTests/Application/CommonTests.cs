using SahaNet.Application.Common;

namespace SahaNet.UnitTests.Application;

public class CommonTests
{
    [Theory]
    [InlineData("OTP 05321234567 numarasına gönderildi", "OTP 5** *** ** 67 numarasına gönderildi")]
    [InlineData("tel: +90 532 123 45 67", "tel: 5** *** ** 67")]
    [InlineData("sipariş 1234567890123", "sipariş 1234567890123")]
    [InlineData("", "")]
    public void PhoneMasking_MasksTurkishMobileNumbers(string input, string expected)
    {
        PhoneMasking.MaskInText(input).Should().Be(expected);
    }

    [Fact]
    public void RegionCatalog_Has81Provinces()
    {
        RegionCatalog.All.Should().HaveCount(81);
        RegionCatalog.Find("34")!.Name.Should().Be("İstanbul");
        RegionCatalog.Find("81")!.Name.Should().Be("Düzce");
        RegionCatalog.Find("82").Should().BeNull();
    }
}
