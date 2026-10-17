using System.Net;
using System.Text;
using SahaNet.Mobile.Core.Api;
using SahaNet.Mobile.Core.Localization;
using SahaNet.Mobile.Core.Niche;
using SahaNet.Mobile.Core.ViewModels;

namespace SahaNet.UnitTests.Mobile;

public class MobileViewModelTests
{
    private static readonly NicheProfile Fishing = new("fishing", "BalıkçıNet", "#0D47A1", "57", "sinop", [new("sea", "Deniz durumu", "🌊", "sea")]);

    [Fact]
    public async Task Onboarding_ThreeSteps_RecordsAllConsentDecisions()
    {
        var api = new FakeApi();
        var vm = new OnboardingViewModel(api, Fishing);
        var completed = false;
        vm.Completed += (_, _) => completed = true;

        vm.SendCodeCommand.CanExecute(null).Should().BeFalse();
        vm.Phone = "0532 123 45 67";
        await vm.SendCodeCommand.ExecuteAsync(null);
        vm.Step.Should().Be(OnboardingStep.Code);

        vm.Code = "123456";
        await vm.VerifyCommand.ExecuteAsync(null);
        vm.Step.Should().Be(OnboardingStep.Profile);
        vm.SelectedRegion!.Code.Should().Be("57", "niş profilindeki varsayılan bölge önceden seçilir");

        await vm.CompleteCommand.ExecuteAsync(null);
        vm.ErrorMessage.Should().Be(Strings.Get("Onboarding_ConsentsRequired"));

        vm.Consents.Single(c => c.Document.IsRequired).Accepted = true;
        await vm.CompleteCommand.ExecuteAsync(null);

        completed.Should().BeTrue();
        api.Onboarded.Should().Be(("fishing", "57"));
        api.RecordedDecisions.Should().HaveCount(2).And.Contain(d => !d.Granted, "konum rızası açıkça 'verilmedi' kaydedilir");
    }

    [Fact]
    public async Task Onboarding_WrongCode_ShowsError_AndStaysOnStep()
    {
        var vm = new OnboardingViewModel(new FakeApi(), Fishing) { Phone = "05321234567" };
        await vm.SendCodeCommand.ExecuteAsync(null);

        vm.Code = "000000";
        await vm.VerifyCommand.ExecuteAsync(null);

        vm.Step.Should().Be(OnboardingStep.Code);
        vm.ErrorMessage.Should().Be("Kod hatalı.");
    }

    [Fact]
    public async Task Onboarding_ExistingUser_SkipsProfileStep()
    {
        var vm = new OnboardingViewModel(new FakeApi { RequiresOnboarding = false }, Fishing) { Phone = "05321234567", Code = "123456" };

        await vm.VerifyCommand.ExecuteAsync(null);

        vm.Step.Should().Be(OnboardingStep.Done);
    }

    [Theory]
    [InlineData("2,5", 2.5)]
    [InlineData("2.5 kg", 2.5)]
    [InlineData(" 12 ", 12.0)]
    [InlineData("", null)]
    [InlineData("iki", null)]
    [InlineData("0", null)]
    public void ParseNumber_AcceptsTurkishAndVoiceInput(string text, double? expected)
    {
        CatchLogViewModel.ParseNumber(text).Should().Be(expected);
    }

    [Fact]
    public void NicheProfile_LoadsFromJson_AndValidates()
    {
        const string json = """{"key":"fishing","appName":"BalıkçıNet","primaryColor":"#0D47A1","defaultRegionCode":"34","defaultPortCode":"sariyer","tiles":[{"key":"sea","title":"Deniz","icon":"🌊","route":"sea"}]}""";

        var profile = NicheProfile.Load(new MemoryStream(Encoding.UTF8.GetBytes(json)));

        profile.AppName.Should().Be("BalıkçıNet");
        profile.Tiles.Should().ContainSingle();
        var invalid = () => NicheProfile.Load(new MemoryStream(Encoding.UTF8.GetBytes("""{"key":"x","tiles":[]}""")));
        invalid.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Strings_DefaultTurkish_WithEnglishSatellite()
    {
        Strings.Get("Sea_NoGo", new System.Globalization.CultureInfo("tr")).Should().Be("Denize çıkmayın");
        Strings.Get("Sea_NoGo", new System.Globalization.CultureInfo("en")).Should().Be("Do not go out");
        Strings.Get("Missing_Key", null).Should().Be("Missing_Key");
    }

    private sealed class ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string?> AuthHeaders { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AuthHeaders.Add(request.Headers.Authorization?.Parameter);
            return Task.FromResult(respond(request));
        }
    }

    [Fact]
    public async Task AuthHandler_RefreshesOnceOn401_AndRetries()
    {
        var tokens = new InMemoryTokenStore();
        await tokens.SetAsync(new StoredTokens("old", DateTimeOffset.UtcNow, "refresh-1"));

        using var api = new ScriptedHandler(r => r.Headers.Authorization?.Parameter == "new"
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"userId":"8f0c4d7e-7d6a-4c39-9a1e-0f8b8b6f2a11","nicheKey":"fishing","isOnboarded":true,"phone":"+90"}""", Encoding.UTF8, "application/json") }
            : new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var refresh = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"userId":"8f0c4d7e-7d6a-4c39-9a1e-0f8b8b6f2a11","accessToken":"new","accessTokenExpiresAt":"2030-01-01T00:00:00Z","refreshToken":"refresh-2","refreshTokenExpiresAt":"2030-01-01T00:00:00Z","isNewUser":false,"requiresOnboarding":false}""", Encoding.UTF8, "application/json"),
        });

        using var handler = new AuthHandler(tokens, () => new HttpClient(refresh, disposeHandler: false) { BaseAddress = new Uri("https://api.test/") }) { InnerHandler = api };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") };
        var client = new SahaNetApiClient(http, tokens);

        var profile = await client.GetProfileAsync(CancellationToken.None);

        profile.IsSuccess.Should().BeTrue();
        api.AuthHeaders.Should().Equal("old", "new");
        (await tokens.GetAsync())!.RefreshToken.Should().Be("refresh-2", "yenileme belirteci döndürülür");
    }

    [Fact]
    public async Task AuthHandler_FailedRefresh_ClearsSession()
    {
        var tokens = new InMemoryTokenStore();
        await tokens.SetAsync(new StoredTokens("old", DateTimeOffset.UtcNow, "revoked"));
        using var api = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var refresh = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var handler = new AuthHandler(tokens, () => new HttpClient(refresh, disposeHandler: false) { BaseAddress = new Uri("https://api.test/") }) { InnerHandler = api };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") };

        var result = await new SahaNetApiClient(http, tokens).GetProfileAsync(CancellationToken.None);

        result.Outcome.Should().Be(ApiOutcome.Unauthorized);
        (await tokens.GetAsync()).Should().BeNull();
    }

    [Fact]
    public async Task ApiClient_NetworkFailure_IsTransient()
    {
        using var failing = new ScriptedHandler(_ => throw new HttpRequestException("no route"));
        using var http = new HttpClient(failing) { BaseAddress = new Uri("https://api.test/") };

        var result = await new SahaNetApiClient(http, new InMemoryTokenStore()).GetSpeciesAsync(CancellationToken.None);

        result.Outcome.Should().Be(ApiOutcome.Transient);
        result.ErrorMessage.Should().Contain("İnternet");
    }
}
