using SahaNet.Application.Identity.Auth;
using SahaNet.Application.Options;
using SahaNet.Domain.Consents;
using SahaNet.Domain.Identity;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace SahaNet.UnitTests.Fakes;

/// <summary>Kimlik handler'larını sahte bağımlılıklarla kurar.</summary>
internal sealed class IdentityHarness
{
    public const string ValidPhone = "0532 123 45 67";

    public FakeClock Clock { get; } = new();

    public FakeProtector Protector { get; } = new();

    public FakeOtpGenerator OtpGenerator { get; } = new();

    public FakeSmsSender Sms { get; } = new();

    public FakeUnitOfWork UnitOfWork { get; } = new();

    public FakeCurrentUser CurrentUser { get; } = new();

    public FakePublisher Publisher { get; } = new();

    public InMemoryUsers Users { get; } = new();

    public InMemoryOtpChallenges Challenges { get; } = new();

    public InMemoryRefreshTokens RefreshTokens { get; } = new();

    public InMemoryConsents Consents { get; } = new();

    public IdentityOptions Settings { get; } = new();

    public FakeModule[] Modules { get; } = [new("fishing")];

    public ConsentDocument PrivacyNotice { get; }

    public ConsentDocument ExplicitConsent { get; }

    public ConsentDocument LocationSharing { get; }

    public IdentityHarness()
    {
        PrivacyNotice = new ConsentDocument(Guid.NewGuid(), ConsentType.PrivacyNotice, "1.0", "Aydınlatma", "...", isRequired: true, Clock.UtcNow);
        ExplicitConsent = new ConsentDocument(Guid.NewGuid(), ConsentType.ExplicitConsent, "1.0", "Açık rıza", "...", isRequired: true, Clock.UtcNow);
        LocationSharing = new ConsentDocument(Guid.NewGuid(), ConsentType.LocationSharing, "1.0", "Konum", "...", isRequired: false, Clock.UtcNow);
        Consents.Documents.AddRange([PrivacyNotice, ExplicitConsent, LocationSharing]);
    }

    public Microsoft.Extensions.Options.IOptions<IdentityOptions> Options => MsOptions.Create(Settings);

    public TokenIssuer TokenIssuer => new(new FakeTokenService(Clock), RefreshTokens, Protector, Clock, Options);

    public User AddUser(bool onboarded = false)
    {
        var phone = PhoneNumber.Create(ValidPhone).Value;
        var user = User.Register(phone, Protector.ComputeLookupHash(phone.Value), Clock.UtcNow);
        if (onboarded)
        {
            user.CompleteOnboarding("fishing", "34", null);
        }

        Users.Add(user);
        CurrentUser.UserId = user.Id;
        return user;
    }

    public void GrantRequiredConsents(User user)
    {
        Consents.Add(UserConsent.Record(user.Id, PrivacyNotice, granted: true, "mobile", Clock.UtcNow));
        Consents.Add(UserConsent.Record(user.Id, ExplicitConsent, granted: true, "mobile", Clock.UtcNow));
    }
}
