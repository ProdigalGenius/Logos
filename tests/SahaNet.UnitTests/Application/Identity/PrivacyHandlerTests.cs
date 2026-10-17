using SahaNet.Application.Identity.Privacy;
using SahaNet.Domain.Identity;
using SahaNet.UnitTests.Fakes;

namespace SahaNet.UnitTests.Application.Identity;

public class PrivacyHandlerTests
{
    private readonly IdentityHarness _h = new();

    [Fact]
    public async Task RequestDeletion_SchedulesPurge_AndRevokesSessions()
    {
        var user = _h.AddUser(onboarded: true);
        _h.RefreshTokens.Add(RefreshToken.Issue(user.Id, "h:t1", Guid.NewGuid(), _h.Clock.UtcNow, TimeSpan.FromDays(30)));
        var handler = new RequestAccountDeletionCommandHandler(_h.CurrentUser, _h.Users, _h.RefreshTokens, _h.UnitOfWork, _h.Clock, _h.Options);

        var result = await handler.Handle(new RequestAccountDeletionCommand(), CancellationToken.None);

        result.Value.ScheduledFor.Should().Be(_h.Clock.UtcNow + _h.Settings.AccountDeletionGracePeriod);
        user.Status.Should().Be(UserStatus.PendingDeletion);
        _h.RefreshTokens.Items.Should().OnlyContain(t => t.IsRevoked);
    }

    [Fact]
    public async Task CancelDeletion_RestoresAccount()
    {
        var user = _h.AddUser();
        user.RequestDeletion(_h.Clock.UtcNow, _h.Settings.AccountDeletionGracePeriod);

        var result = await new CancelAccountDeletionCommandHandler(_h.CurrentUser, _h.Users, _h.UnitOfWork)
            .Handle(new CancelAccountDeletionCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public async Task Purge_RemovesOnlyAccountsPastGracePeriod()
    {
        var due = _h.AddUser();
        due.RequestDeletion(_h.Clock.UtcNow, _h.Settings.AccountDeletionGracePeriod);
        _h.Clock.Advance(TimeSpan.FromDays(10));
        var phone = PhoneNumber.Create("05551112233").Value;
        var notYet = User.Register(phone, "h:other", _h.Clock.UtcNow);
        notYet.RequestDeletion(_h.Clock.UtcNow, _h.Settings.AccountDeletionGracePeriod);
        _h.Users.Add(notYet);
        _h.Clock.Advance(TimeSpan.FromDays(20));

        var purged = await new PurgeDeletedAccountsCommandHandler(_h.Users, _h.UnitOfWork, _h.Clock, _h.Options)
            .Handle(new PurgeDeletedAccountsCommand(), CancellationToken.None);

        purged.Should().Be(1);
        _h.Users.Items.Should().ContainSingle().Which.Should().Be(notYet);
        due.DomainEvents.OfType<UserPurged>().Should().ContainSingle();
    }

    [Fact]
    public async Task Export_ContainsProfileAndConsents()
    {
        var user = _h.AddUser(onboarded: true);
        _h.GrantRequiredConsents(user);

        var result = await new ExportMyDataQueryHandler(_h.CurrentUser, _h.Users, _h.Consents, new InMemorySubscriptions(), new InMemoryRegions(), new InMemoryDevices(), [], _h.Clock)
            .Handle(new ExportMyDataQuery(), CancellationToken.None);

        result.Value.Profile.Phone.Should().Be("+905321234567");
        result.Value.Consents.Should().HaveCount(2);
    }
}
