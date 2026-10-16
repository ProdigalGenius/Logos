using SahaNet.Application.Consents;
using SahaNet.Application.Identity.Profile;
using SahaNet.Domain.Identity;
using SahaNet.UnitTests.Fakes;

namespace SahaNet.UnitTests.Application.Identity;

public class ProfileAndConsentHandlerTests
{
    private readonly IdentityHarness _h = new();

    private CompleteOnboardingCommandHandler OnboardingHandler() =>
        new(_h.CurrentUser, _h.Users, _h.Consents, _h.Modules, _h.UnitOfWork);

    [Fact]
    public async Task Onboarding_WithoutRequiredConsents_Fails()
    {
        _h.AddUser();

        var result = await OnboardingHandler().Handle(new CompleteOnboardingCommand("fishing", "34", null), CancellationToken.None);

        result.Error.Code.Should().Be("consent.required_missing");
    }

    [Theory]
    [InlineData("beekeeping", "34", "identity.niche_unknown")]
    [InlineData("fishing", "99", "identity.region_unknown")]
    public async Task Onboarding_RejectsUnknownNicheOrRegion(string niche, string region, string expectedCode)
    {
        _h.GrantRequiredConsents(_h.AddUser());

        var result = await OnboardingHandler().Handle(new CompleteOnboardingCommand(niche, region, null), CancellationToken.None);

        result.Error.Code.Should().Be(expectedCode);
    }

    [Fact]
    public async Task Onboarding_Succeeds_WithConsents()
    {
        var user = _h.AddUser();
        _h.GrantRequiredConsents(user);

        var result = await OnboardingHandler().Handle(new CompleteOnboardingCommand("fishing", "61", "Kaptan"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RegionName.Should().Be("Trabzon");
        result.Value.IsOnboarded.Should().BeTrue();
        user.NicheKey.Should().Be("fishing");
    }

    [Fact]
    public async Task Handlers_WithoutCurrentUser_ReturnUnauthenticated()
    {
        var result = await new GetMyProfileQueryHandler(_h.CurrentUser, _h.Users).Handle(new GetMyProfileQuery(), CancellationToken.None);

        result.Error.Should().Be(IdentityErrors.Unauthenticated);
    }

    [Fact]
    public async Task UpdateProfile_ChangesNameAndRegion()
    {
        _h.AddUser(onboarded: true);

        var result = await new UpdateProfileCommandHandler(_h.CurrentUser, _h.Users, _h.UnitOfWork)
            .Handle(new UpdateProfileCommand("Yeni Ad", "48"), CancellationToken.None);

        result.Value.DisplayName.Should().Be("Yeni Ad");
        result.Value.RegionName.Should().Be("Muğla");
    }

    [Fact]
    public async Task RecordConsents_AppendsHistory_AndReportsMissing()
    {
        var user = _h.AddUser();
        var handler = new RecordConsentsCommandHandler(_h.CurrentUser, _h.Consents, _h.UnitOfWork, _h.Clock);

        var result = await handler.Handle(
            new RecordConsentsCommand([new ConsentDecision(_h.PrivacyNotice.Id, true), new ConsentDecision(_h.LocationSharing.Id, true)], "mobile"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.History.Should().HaveCount(2);
        result.Value.MissingRequiredDocumentIds.Should().ContainSingle().Which.Should().Be(_h.ExplicitConsent.Id);
        result.Value.LocationSharingGranted.Should().BeTrue();
        _h.Consents.Records.Should().OnlyContain(c => c.UserId == user.Id && c.Version == "1.0");
    }

    [Fact]
    public async Task RecordConsents_UnknownDocument_Fails()
    {
        _h.AddUser();
        var handler = new RecordConsentsCommandHandler(_h.CurrentUser, _h.Consents, _h.UnitOfWork, _h.Clock);

        var result = await handler.Handle(new RecordConsentsCommand([new ConsentDecision(Guid.NewGuid(), true)], "mobile"), CancellationToken.None);

        result.Error.Should().Be(ConsentErrors.DocumentNotFound);
        _h.Consents.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task GetConsentDocuments_ReturnsActiveDocuments()
    {
        var documents = await new GetConsentDocumentsQueryHandler(_h.Consents).Handle(new GetConsentDocumentsQuery(), CancellationToken.None);

        documents.Should().HaveCount(3);
    }
}
