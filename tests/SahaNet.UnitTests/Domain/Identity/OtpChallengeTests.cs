using SahaNet.Domain.Identity;

namespace SahaNet.UnitTests.Domain.Identity;

public class OtpChallengeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 8, 0, 0, TimeSpan.Zero);

    private static OtpChallenge NewChallenge() => OtpChallenge.Create("phone", "good", Now, TimeSpan.FromMinutes(3));

    [Fact]
    public void Verify_CorrectCode_Succeeds_Once()
    {
        var challenge = NewChallenge();

        challenge.Verify("good", Now.AddMinutes(1), 5).Should().Be(OtpVerificationOutcome.Success);
        challenge.Verify("good", Now.AddMinutes(1), 5).Should().Be(OtpVerificationOutcome.AlreadyUsed);
    }

    [Fact]
    public void Verify_WrongCode_CountsAttempts_AndLocks()
    {
        var challenge = NewChallenge();

        for (var i = 0; i < 4; i++)
        {
            challenge.Verify("bad", Now, 5).Should().Be(OtpVerificationOutcome.Invalid);
        }

        challenge.Verify("bad", Now, 5).Should().Be(OtpVerificationOutcome.Locked);
        challenge.Verify("good", Now, 5).Should().Be(OtpVerificationOutcome.Locked, "kilitli kod doğru olsa da kabul edilmez");
        challenge.FailedAttempts.Should().Be(5);
    }

    [Fact]
    public void Verify_AfterExpiry_ReturnsExpired()
    {
        NewChallenge().Verify("good", Now.AddMinutes(3), 5).Should().Be(OtpVerificationOutcome.Expired);
    }
}
