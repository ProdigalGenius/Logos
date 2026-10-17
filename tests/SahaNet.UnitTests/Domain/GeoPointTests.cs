using SahaNet.Domain.Geo;

namespace SahaNet.UnitTests.Domain;

public class GeoPointTests
{
    [Fact]
    public void Distance_IstanbulToIzmir_IsAbout330Km()
    {
        var istanbul = GeoPoint.FromTrusted(41.0082, 28.9784);
        var izmir = GeoPoint.FromTrusted(38.4237, 27.1428);

        istanbul.DistanceKmTo(izmir).Should().BeApproximately(328, 5);
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(0, 181)]
    [InlineData(double.NaN, 0)]
    public void Create_RejectsOutOfRange(double lat, double lon)
    {
        GeoPoint.Create(lat, lon).IsFailure.Should().BeTrue();
    }
}
