using SahaNet.Domain.Geo;
using SahaNet.Modules.Fishing.Domain;

namespace SahaNet.UnitTests.Modules.Fishing;

public class FishingDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(4, 14, false)]
    [InlineData(4, 15, true)]
    [InlineData(7, 1, true)]
    [InlineData(8, 31, true)]
    [InlineData(9, 1, false)]
    public void Ban_WithinYear(int month, int day, bool expected)
    {
        var ban = new FishingBan(Guid.NewGuid(), "Genel", 4, 15, 8, 31, null, null, null, null);

        ban.IsActiveOn(new DateOnly(2026, month, day)).Should().Be(expected);
    }

    [Theory]
    [InlineData(12, 20, true)]
    [InlineData(1, 10, true)]
    [InlineData(2, 16, false)]
    [InlineData(11, 30, false)]
    public void Ban_WrappingYearEnd(int month, int day, bool expected)
    {
        var ban = new FishingBan(Guid.NewGuid(), "Kış", 12, 1, 2, 15, "x", null, null, null);

        ban.IsActiveOn(new DateOnly(2027, month, day)).Should().Be(expected);
    }

    [Fact]
    public void Ban_Applies_ToMatchingSpeciesAndRegion()
    {
        var ban = new FishingBan(Guid.NewGuid(), "Kalkan", 4, 15, 6, 15, "kalkan", "karadeniz-bati", null, null);

        ban.Applies("kalkan", "karadeniz-bati").Should().BeTrue();
        ban.Applies("levrek", null).Should().BeFalse();
        ban.Applies(null, "ege-orta").Should().BeFalse();
        ban.Applies(null, null).Should().BeTrue();
    }

    [Fact]
    public void Species_Undersized()
    {
        var levrek = new Species("levrek", "Levrek", null, 25);

        levrek.IsUndersized(22).Should().BeTrue();
        levrek.IsUndersized(25).Should().BeFalse();
        new Species("kalamar", "Kalamar", null, null).IsUndersized(1).Should().BeFalse();
    }

    [Fact]
    public void Spot_SharingRules()
    {
        var owner = Guid.NewGuid();
        var friend = Guid.NewGuid();
        var spot = CatchSpot.Create(owner, " Taşlık ", GeoPoint.FromTrusted(41.1, 29.05), 18, " not ", ["levrek", "levrek", "lufer"], Now);

        spot.Name.Should().Be("Taşlık");
        spot.SpeciesList.Should().Equal("levrek", "lufer");
        spot.IsVisibleTo(friend).Should().BeFalse();

        spot.ShareWith(owner, Now).Error.Should().Be(FishingErrors.CannotShareWithSelf);
        spot.ShareWith(friend, Now).IsSuccess.Should().BeTrue();
        spot.ShareWith(friend, Now).IsSuccess.Should().BeTrue();
        spot.Shares.Should().ContainSingle();
        spot.IsVisibleTo(friend).Should().BeTrue();

        spot.UnshareAll();
        spot.IsVisibleTo(friend).Should().BeFalse();
    }

    [Fact]
    public void Catch_InFuture_IsRejected()
    {
        CatchEntry.Record(Guid.NewGuid(), Now.AddHours(1), "levrek", 2, null, null, null, null, null, Now)
            .Error.Should().Be(FishingErrors.CatchInFuture);
    }

    [Theory]
    [InlineData(2026, 9, 1, 2026)]
    [InlineData(2026, 8, 31, 2025)]
    [InlineData(2027, 1, 15, 2026)]
    public void Season_StartsInSeptember(int y, int m, int d, int expected)
    {
        CatchEntry.SeasonStartYear(new DateOnly(y, m, d)).Should().Be(expected);
    }
}
