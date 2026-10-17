using SahaNet.Application.Identity.Auth;
using SahaNet.Domain.Identity;
using SahaNet.UnitTests.Fakes;

namespace SahaNet.UnitTests.Application.Identity;

public class AuthHandlerTests
{
    private readonly IdentityHarness _h = new();

    private RequestOtpCommandHandler RequestHandler() =>
        new(_h.Challenges, _h.Protector, _h.OtpGenerator, _h.Sms, _h.UnitOfWork, _h.Clock, _h.Options);

    private VerifyOtpCommandHandler VerifyHandler() =>
        new(_h.Challenges, _h.Users, _h.Protector, _h.TokenIssuer, _h.UnitOfWork, _h.Clock, _h.Options);

    private RefreshSessionCommandHandler RefreshHandler() =>
        new(_h.RefreshTokens, _h.Users, _h.Protector, _h.TokenIssuer, _h.UnitOfWork, _h.Clock);

    private async Task<AuthResponse> LoginAsync()
    {
        await RequestHandler().Handle(new RequestOtpCommand(IdentityHarness.ValidPhone), CancellationToken.None);
        return (await VerifyHandler().Handle(new VerifyOtpCommand(IdentityHarness.ValidPhone, "123456"), CancellationToken.None)).Value;
    }

    [Fact]
    public async Task RequestOtp_SendsSms_WithoutStoringPlainCode()
    {
        var result = await RequestHandler().Handle(new RequestOtpCommand(IdentityHarness.ValidPhone), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _h.Sms.Sent.Should().ContainSingle().Which.Message.Should().Contain("123456");
        _h.Challenges.Items.Should().ContainSingle();
        _h.Challenges.Items[0].CodeHash.Should().NotBe("123456");
    }

    [Fact]
    public async Task RequestOtp_InvalidPhone_Fails()
    {
        var result = await RequestHandler().Handle(new RequestOtpCommand("12345"), CancellationToken.None);

        result.Error.Should().Be(IdentityErrors.PhoneInvalid);
        _h.Sms.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task RequestOtp_EnforcesCooldown_AndHourlyLimit()
    {
        var handler = RequestHandler();
        var command = new RequestOtpCommand(IdentityHarness.ValidPhone);

        (await handler.Handle(command, CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await handler.Handle(command, CancellationToken.None)).Error.Should().Be(IdentityErrors.OtpCooldown);

        for (var i = 1; i < _h.Settings.OtpMaxPerHour; i++)
        {
            _h.Clock.Advance(_h.Settings.OtpResendCooldown);
            (await handler.Handle(command, CancellationToken.None)).IsSuccess.Should().BeTrue();
        }

        _h.Clock.Advance(_h.Settings.OtpResendCooldown);
        (await handler.Handle(command, CancellationToken.None)).Error.Should().Be(IdentityErrors.OtpHourlyLimit);
    }

    [Fact]
    public async Task VerifyOtp_NewUser_RegistersAndIssuesTokens()
    {
        var auth = await LoginAsync();

        auth.IsNewUser.Should().BeTrue();
        auth.RequiresOnboarding.Should().BeTrue();
        _h.Users.Items.Should().ContainSingle();
        _h.RefreshTokens.Items.Should().ContainSingle().Which.TokenHash.Should().Be("h:" + auth.RefreshToken, "yalnızca özet saklanır");
    }

    [Fact]
    public async Task VerifyOtp_ExistingUser_IsNotDuplicated()
    {
        await LoginAsync();
        _h.Clock.Advance(_h.Settings.OtpResendCooldown);

        var second = await LoginAsync();

        second.IsNewUser.Should().BeFalse();
        _h.Users.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task VerifyOtp_WrongCode_PersistsAttempt_AndLocks()
    {
        await RequestHandler().Handle(new RequestOtpCommand(IdentityHarness.ValidPhone), CancellationToken.None);
        var handler = VerifyHandler();
        var savesBefore = _h.UnitOfWork.SaveCount;

        for (var i = 1; i < _h.Settings.OtpMaxAttempts; i++)
        {
            (await handler.Handle(new VerifyOtpCommand(IdentityHarness.ValidPhone, "000000"), CancellationToken.None))
                .Error.Should().Be(IdentityErrors.OtpInvalid);
        }

        (await handler.Handle(new VerifyOtpCommand(IdentityHarness.ValidPhone, "000000"), CancellationToken.None))
            .Error.Should().Be(IdentityErrors.OtpLocked);
        (await handler.Handle(new VerifyOtpCommand(IdentityHarness.ValidPhone, "123456"), CancellationToken.None))
            .Error.Should().Be(IdentityErrors.OtpLocked);

        _h.UnitOfWork.SaveCount.Should().BeGreaterThan(savesBefore);
        _h.Users.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyOtp_Expired_Fails()
    {
        await RequestHandler().Handle(new RequestOtpCommand(IdentityHarness.ValidPhone), CancellationToken.None);
        _h.Clock.Advance(_h.Settings.OtpLifetime);

        var result = await VerifyHandler().Handle(new VerifyOtpCommand(IdentityHarness.ValidPhone, "123456"), CancellationToken.None);

        result.Error.Should().Be(IdentityErrors.OtpExpired);
    }

    [Fact]
    public async Task VerifyOtp_WithoutChallenge_Fails()
    {
        var result = await VerifyHandler().Handle(new VerifyOtpCommand(IdentityHarness.ValidPhone, "123456"), CancellationToken.None);

        result.Error.Should().Be(IdentityErrors.OtpInvalid);
    }

    [Fact]
    public async Task Refresh_RotatesToken()
    {
        var auth = await LoginAsync();

        var refreshed = await RefreshHandler().Handle(new RefreshSessionCommand(auth.RefreshToken), CancellationToken.None);

        refreshed.IsSuccess.Should().BeTrue();
        refreshed.Value.RefreshToken.Should().NotBe(auth.RefreshToken);
        _h.RefreshTokens.Items.Should().HaveCount(2);
        _h.RefreshTokens.Items[0].IsRevoked.Should().BeTrue();
        _h.RefreshTokens.Items[0].ReplacedById.Should().Be(_h.RefreshTokens.Items[1].Id);
    }

    [Fact]
    public async Task Refresh_ReusingRotatedToken_RevokesWholeFamily()
    {
        var auth = await LoginAsync();
        var handler = RefreshHandler();
        var rotated = (await handler.Handle(new RefreshSessionCommand(auth.RefreshToken), CancellationToken.None)).Value;

        var reuse = await handler.Handle(new RefreshSessionCommand(auth.RefreshToken), CancellationToken.None);

        reuse.Error.Should().Be(IdentityErrors.RefreshTokenInvalid);
        _h.RefreshTokens.Items.Should().OnlyContain(t => t.IsRevoked);
        (await handler.Handle(new RefreshSessionCommand(rotated.RefreshToken), CancellationToken.None))
            .Error.Should().Be(IdentityErrors.RefreshTokenInvalid);
    }

    [Fact]
    public async Task Refresh_ExpiredOrUnknown_Fails()
    {
        var auth = await LoginAsync();
        var handler = RefreshHandler();

        (await handler.Handle(new RefreshSessionCommand("unknown"), CancellationToken.None))
            .Error.Should().Be(IdentityErrors.RefreshTokenInvalid);

        _h.Clock.Advance(_h.Settings.RefreshTokenLifetime);
        (await handler.Handle(new RefreshSessionCommand(auth.RefreshToken), CancellationToken.None))
            .Error.Should().Be(IdentityErrors.RefreshTokenInvalid);
    }

    [Fact]
    public async Task Logout_RevokesFamily()
    {
        var auth = await LoginAsync();

        var result = await new LogoutCommandHandler(_h.RefreshTokens, _h.Protector, _h.UnitOfWork, _h.Clock)
            .Handle(new LogoutCommand(auth.RefreshToken), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _h.RefreshTokens.Items.Should().OnlyContain(t => t.IsRevoked);
    }

    [Fact]
    public async Task VerifyOtp_SuspendedUser_IsRejected()
    {
        await LoginAsync();
        _h.Users.Items[0].Suspend("Dolandırıcılık");
        _h.Clock.Advance(_h.Settings.OtpResendCooldown);
        await RequestHandler().Handle(new RequestOtpCommand(IdentityHarness.ValidPhone), CancellationToken.None);

        var result = await VerifyHandler().Handle(new VerifyOtpCommand(IdentityHarness.ValidPhone, "123456"), CancellationToken.None);

        result.Error.Should().Be(IdentityErrors.AccountSuspended);
    }
}
