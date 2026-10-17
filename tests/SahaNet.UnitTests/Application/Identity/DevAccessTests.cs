using SahaNet.Application.Identity.Auth;
using SahaNet.Application.Moderation;
using SahaNet.Application.Options;
using SahaNet.Domain.Identity;
using SahaNet.Domain.Moderation;
using SahaNet.UnitTests.Fakes;

namespace SahaNet.UnitTests.Application.Identity;

public class DevAccessTests
{
    private readonly IdentityHarness _h = new();
    private readonly FakeCurrentUser _current = new();
    private readonly RecordingAudit _audit = new();

    public DevAccessTests() => _h.Settings = new IdentityOptions { AdminPhones = [IdentityHarness.ValidPhone] };

    private PasswordlessLoginCommandHandler LoginHandler() =>
        new(_h.Users, _h.Protector, _h.TokenIssuer, _h.UnitOfWork, _h.Clock, _h.Options);

    private CreateUserCommandHandler CreateHandler() =>
        new(_current, _h.Users, _h.Protector, _audit, _h.UnitOfWork, _h.Clock);

    [Fact]
    public async Task PasswordlessLogin_RegistersOnce_AndIssuesTokens()
    {
        var first = await LoginHandler().Handle(new PasswordlessLoginCommand(IdentityHarness.ValidPhone), CancellationToken.None);
        var second = await LoginHandler().Handle(new PasswordlessLoginCommand(IdentityHarness.ValidPhone), CancellationToken.None);

        first.Value.IsNewUser.Should().BeTrue();
        _h.Users.Items[0].Role.Should().Be(UserRole.Admin);
        second.Value.IsNewUser.Should().BeFalse();
        _h.Users.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task PasswordlessLogin_NonAdminPhone_IsRejected()
    {
        (await LoginHandler().Handle(new PasswordlessLoginCommand("0533 000 00 00"), CancellationToken.None)).IsFailure.Should().BeTrue();
        _h.Users.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task PasswordlessLogin_InvalidPhone_Fails() =>
        (await LoginHandler().Handle(new PasswordlessLoginCommand("123"), CancellationToken.None)).Error.Should().Be(IdentityErrors.PhoneInvalid);

    [Fact]
    public async Task PasswordlessLogin_SuspendedUser_IsRejected()
    {
        await LoginHandler().Handle(new PasswordlessLoginCommand(IdentityHarness.ValidPhone), CancellationToken.None);
        _h.Users.Items[0].Suspend("test");

        (await LoginHandler().Handle(new PasswordlessLoginCommand(IdentityHarness.ValidPhone), CancellationToken.None))
            .Error.Should().Be(IdentityErrors.AccountSuspended);
    }

    [Fact]
    public async Task CreateUser_RequiresAdmin()
    {
        _current.UserId = Guid.NewGuid();

        (await CreateHandler().Handle(new CreateUserCommand(IdentityHarness.ValidPhone, null), CancellationToken.None))
            .Error.Should().Be(ModerationErrors.AdminOnly);
        _h.Users.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateUser_AddsUserWithRole_AndAudits()
    {
        _current.UserId = Guid.NewGuid();
        _current.IsAdmin = true;

        var created = await CreateHandler().Handle(new CreateUserCommand(IdentityHarness.ValidPhone, "Moderator"), CancellationToken.None);
        var again = await CreateHandler().Handle(new CreateUserCommand(IdentityHarness.ValidPhone, null), CancellationToken.None);

        created.Value.AlreadyExisted.Should().BeFalse();
        created.Value.Role.Should().Be("Moderator");
        again.Value.AlreadyExisted.Should().BeTrue();
        _h.Users.Items.Should().ContainSingle().Which.Role.Should().Be(UserRole.Moderator);
        _audit.Actions.Should().Equal("user.create", "user.update");
    }

    private sealed class RecordingAudit : IModerationService
    {
        public List<string> Actions { get; } = [];

        public Task FlagAsync(ReportTargetType type, Guid targetId, Guid ownerId, SpamResult spam, CancellationToken cancellationToken) => Task.CompletedTask;

        public void Audit(Guid actorId, string action, string targetType, string targetId, string? details) => Actions.Add(action);
    }
}
