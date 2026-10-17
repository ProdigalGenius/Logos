using SahaNet.Domain.Consents;

namespace SahaNet.UnitTests.Domain;

public class ConsentPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    private static ConsentDocument Doc(ConsentType type, string version, bool required) =>
        new(Guid.NewGuid(), type, version, type.ToString(), "...", required, Now);

    [Fact]
    public void MissingRequired_ListsUngrantedRequiredDocuments()
    {
        var notice = Doc(ConsentType.PrivacyNotice, "1.0", true);
        var explicitConsent = Doc(ConsentType.ExplicitConsent, "1.0", true);
        var location = Doc(ConsentType.LocationSharing, "1.0", false);
        var given = new[] { UserConsent.Record(UserId, notice, true, "mobile", Now) };

        var missing = ConsentPolicy.MissingRequired([notice, explicitConsent, location], given);

        missing.Should().ContainSingle().Which.Should().Be(explicitConsent);
    }

    [Fact]
    public void LatestDecisionWins_WithdrawalCountsAsMissing()
    {
        var notice = Doc(ConsentType.PrivacyNotice, "1.0", true);
        var given = new[]
        {
            UserConsent.Record(UserId, notice, true, "mobile", Now),
            UserConsent.Record(UserId, notice, false, "mobile", Now.AddMinutes(1)),
        };

        ConsentPolicy.MissingRequired([notice], given).Should().ContainSingle();
    }

    [Fact]
    public void NewVersion_RequiresNewConsent()
    {
        var v1 = Doc(ConsentType.PrivacyNotice, "1.0", true);
        var v2 = Doc(ConsentType.PrivacyNotice, "2.0", true);
        v1.Retire();
        var given = new[] { UserConsent.Record(UserId, v1, true, "mobile", Now) };

        ConsentPolicy.MissingRequired([v1, v2], given).Should().ContainSingle().Which.Should().Be(v2);
    }

    [Fact]
    public void LocationSharing_IsNotGrantedByDefault()
    {
        var location = Doc(ConsentType.LocationSharing, "1.0", false);

        ConsentPolicy.IsGranted(ConsentType.LocationSharing, [location], []).Should().BeFalse();
        ConsentPolicy.IsGranted(ConsentType.LocationSharing, [location], [UserConsent.Record(UserId, location, true, "mobile", Now)])
            .Should().BeTrue();
    }
}
