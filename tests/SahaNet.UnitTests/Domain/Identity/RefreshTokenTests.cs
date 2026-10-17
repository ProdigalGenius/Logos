using SahaNet.Domain.Identity;

namespace SahaNet.UnitTests.Domain.Identity;

public class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsActive_UntilExpiryOrRevocation()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", Guid.NewGuid(), Now, TimeSpan.FromDays(1));

        token.IsActive(Now.AddHours(23)).Should().BeTrue();
        token.IsActive(Now.AddDays(1)).Should().BeFalse();

        var replacement = Guid.NewGuid();
        token.Revoke(Now.AddHours(1), replacement);
        token.Revoke(Now.AddHours(2));

        token.IsActive(Now.AddHours(2)).Should().BeFalse();
        token.RevokedAt.Should().Be(Now.AddHours(1), "ikinci iptal ilk kaydı değiştirmez");
        token.ReplacedById.Should().Be(replacement);
    }
}
