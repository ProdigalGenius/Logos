using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SahaNet.Application.Consents;
using SahaNet.Application.Identity.Auth;
using SahaNet.Application.Identity.Privacy;
using SahaNet.Application.Identity.Profile;
using SahaNet.Infrastructure.Persistence;

namespace SahaNet.IntegrationTests;

[Collection(ApiCollection.Name)]
public class IdentityFlowTests(SahaNetApiFactory factory)
{
    private static async Task GrantRequiredConsentsAsync(HttpClient client)
    {
        var documents = await client.GetFromJsonAsync<List<ConsentDocumentResponse>>("/api/consents/documents");
        var decisions = documents!.Where(d => d.IsRequired).Select(d => new ConsentDecision(d.Id, true)).ToList();
        var response = await client.PostAsJsonAsync("/api/me/consents", new RecordConsentsCommand(decisions, "mobile"));
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task FullOnboardingFlow_Works()
    {
        var (client, auth) = await factory.LoginAsync();
        auth.IsNewUser.Should().BeTrue();
        auth.RequiresOnboarding.Should().BeTrue();

        var early = await client.PostAsJsonAsync("/api/me/onboarding", new CompleteOnboardingCommand("fishing", "48", "Reis"));
        early.StatusCode.Should().Be(HttpStatusCode.BadRequest, "zorunlu rızalar verilmeden kurulum tamamlanamaz");

        await GrantRequiredConsentsAsync(client);
        var onboarding = await client.PostAsJsonAsync("/api/me/onboarding", new CompleteOnboardingCommand("fishing", "48", "Reis"));
        onboarding.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await client.GetFromJsonAsync<ProfileResponse>("/api/me");
        profile!.NicheKey.Should().Be("fishing");
        profile.RegionName.Should().Be("Muğla");
        profile.IsOnboarded.Should().BeTrue();

        var consents = await client.GetFromJsonAsync<MyConsentsResponse>("/api/me/consents");
        consents!.MissingRequiredDocumentIds.Should().BeEmpty();
        consents.LocationSharingGranted.Should().BeFalse("konum paylaşımı varsayılan olarak kapalı");
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync(new Uri("/api/me", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RequestOtp_InvalidBody_Returns400()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/otp/request", new { phone = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RequestOtp_TwiceQuickly_Returns429()
    {
        var client = factory.CreateClient();
        var phone = SahaNetApiFactory.NewPhone();

        (await client.PostAsJsonAsync("/api/auth/otp/request", new { phone })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/auth/otp/request", new { phone })).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task WrongOtp_LocksAfterMaxAttempts()
    {
        var client = factory.CreateClient();
        var phone = SahaNetApiFactory.NewPhone();
        await client.PostAsJsonAsync("/api/auth/otp/request", new { phone });

        for (var i = 0; i < 4; i++)
        {
            (await client.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code = "000000" }))
                .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        (await client.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code = "000000" }))
            .StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await client.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code = factory.Sms.LastCodeFor(phone) }))
            .StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task RefreshToken_Rotates_AndReuseRevokesFamily()
    {
        var (client, auth) = await factory.LoginAsync();

        var first = await client.PostAsJsonAsync("/api/auth/token/refresh", new { refreshToken = auth.RefreshToken });
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = (await first.Content.ReadFromJsonAsync<AuthResponse>())!;

        var reuse = await client.PostAsJsonAsync("/api/auth/token/refresh", new { refreshToken = auth.RefreshToken });
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var afterReuse = await client.PostAsJsonAsync("/api/auth/token/refresh", new { refreshToken = rotated.RefreshToken });
        afterReuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "çalınma şüphesinde tüm aile iptal edilir");
    }

    [Fact]
    public async Task PhoneNumber_IsEncryptedAtRest()
    {
        var phone = SahaNetApiFactory.NewPhone();
        var (_, auth) = await factory.LoginAsync(phone);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SahaNetDbContext>();
        var raw = await db.Database
            .SqlQuery<string>($"SELECT phone_encrypted AS \"Value\" FROM identity.users WHERE \"Id\" = {auth.UserId}")
            .SingleAsync();

        raw.Should().StartWith("v1:").And.NotContain(phone[1..]);
        (await db.Users.SingleAsync(u => u.Id == auth.UserId)).Phone.Value.Should().Be("+90" + phone[1..]);
    }

    [Fact]
    public async Task ExportAndDeletion_Work()
    {
        var (client, auth) = await factory.LoginAsync();
        await GrantRequiredConsentsAsync(client);

        var export = await client.GetFromJsonAsync<PersonalDataExport>("/api/me/export");
        export!.Profile.UserId.Should().Be(auth.UserId);
        export.Consents.Should().NotBeEmpty();

        var deletion = await client.DeleteAsync(new Uri("/api/me", UriKind.Relative));
        deletion.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var refresh = await client.PostAsJsonAsync("/api/auth/token/refresh", new { refreshToken = auth.RefreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "silme talebi tüm oturumları kapatır");

        var cancel = await client.PostAsync(new Uri("/api/me/deletion/cancel", UriKind.Relative), null);
        cancel.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Catalog_IsPublic()
    {
        var client = factory.CreateClient();

        var regions = await client.GetFromJsonAsync<List<SahaNet.Application.Common.Region>>("/api/regions");
        var documents = await client.GetFromJsonAsync<List<ConsentDocumentResponse>>("/api/consents/documents");

        regions.Should().HaveCount(81);
        documents.Should().Contain(d => d.Type == "PrivacyNotice" && d.IsRequired);
    }
}
