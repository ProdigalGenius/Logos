using SahaNet.Modules.Scrap.Domain;

namespace SahaNet.UnitTests.Modules.Scrap;

public class PickupRequestTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Requester = Guid.NewGuid();
    private static readonly Guid CollectorA = Guid.NewGuid();
    private static readonly Guid CollectorB = Guid.NewGuid();

    private static PickupRequest New() =>
        PickupRequest.Create(Requester, "34", " Kadıköy ", "Moda Cad. No:1 D:3", ["demir", "demir", "bakir"], 50, null, Now);

    [Fact]
    public void Create_NormalizesMaterials_AndHidesAddressFromOthers()
    {
        var pickup = New();

        pickup.Materials.Should().Equal("demir", "bakir");
        pickup.District.Should().Be("Kadıköy");
        pickup.CanSeeAddress(Requester).Should().BeTrue();
        pickup.CanSeeAddress(CollectorA).Should().BeFalse("açık adres yalnızca üstlenen hurdacıya görünür");
    }

    [Fact]
    public void Claim_IsFirstComeFirstServed_AndRevealsAddress()
    {
        var pickup = New();

        pickup.Claim(Requester, Now).Error.Should().Be(ScrapErrors.OwnPickup);
        pickup.Claim(CollectorA, Now).IsSuccess.Should().BeTrue();
        pickup.Claim(CollectorB, Now).Error.Should().Be(ScrapErrors.PickupNotOpen);

        pickup.CanSeeAddress(CollectorA).Should().BeTrue();
        pickup.CanSeeAddress(CollectorB).Should().BeFalse();
    }

    [Fact]
    public void Release_ReopensForOthers_AndHidesAddressAgain()
    {
        var pickup = New();
        pickup.Claim(CollectorA, Now);

        pickup.Release(CollectorB).Error.Should().Be(ScrapErrors.NotCollector);
        pickup.Release(CollectorA).IsSuccess.Should().BeTrue();

        pickup.Status.Should().Be(PickupStatus.Open);
        pickup.CanSeeAddress(CollectorA).Should().BeFalse();
        pickup.Claim(CollectorB, Now).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Complete_OnlyByCollector_AndCancelOnlyByRequester()
    {
        var pickup = New();
        pickup.Claim(CollectorA, Now);

        pickup.Complete(CollectorB, Now).Error.Should().Be(ScrapErrors.NotCollector);
        pickup.Cancel(CollectorA, Now).Error.Should().Be(ScrapErrors.NotRequester);
        pickup.Complete(CollectorA, Now.AddHours(2)).IsSuccess.Should().BeTrue();

        pickup.Status.Should().Be(PickupStatus.Completed);
        pickup.Cancel(Requester, Now).Error.Should().Be(ScrapErrors.PickupNotOpen);
    }
}
