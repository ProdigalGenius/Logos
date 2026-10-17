using SahaNet.Domain.Identity;

namespace SahaNet.UnitTests.Domain.Identity;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 8, 0, 0, TimeSpan.Zero);

    private static User NewUser() => User.Register(PhoneNumber.Create("05321234567").Value, "hash", Now);

    [Fact]
    public void Register_RaisesEvent_AndRequiresOnboarding()
    {
        var user = NewUser();

        user.IsOnboarded.Should().BeFalse();
        user.Status.Should().Be(UserStatus.Active);
        user.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<UserRegistered>();
    }

    [Fact]
    public void CompleteOnboarding_SetsNicheAndRegion()
    {
        var user = NewUser();

        var result = user.CompleteOnboarding("fishing", "34", "  Reis  ");

        result.IsSuccess.Should().BeTrue();
        user.IsOnboarded.Should().BeTrue();
        user.DisplayName.Should().Be("Reis");
        user.DomainEvents.OfType<UserOnboarded>().Should().ContainSingle();
    }

    [Fact]
    public void UpdateProfile_RejectsLongName()
    {
        var result = NewUser().UpdateProfile(new string('a', User.DisplayNameMaxLength + 1), null);

        result.Error.Should().Be(IdentityErrors.DisplayNameInvalid);
    }

    [Fact]
    public void Deletion_CanBeRequestedOnce_AndCancelled()
    {
        var user = NewUser();
        var grace = TimeSpan.FromDays(30);

        user.RequestDeletion(Now, grace).Value.Should().Be(Now + grace);
        user.RequestDeletion(Now, grace).Error.Should().Be(IdentityErrors.AccountPendingDeletion);
        user.IsDueForPurge(Now.AddDays(29), grace).Should().BeFalse();
        user.IsDueForPurge(Now.AddDays(30), grace).Should().BeTrue();

        user.CancelDeletion().IsSuccess.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Active);
        user.CancelDeletion().Error.Should().Be(IdentityErrors.AccountNotPendingDeletion);
    }
}
