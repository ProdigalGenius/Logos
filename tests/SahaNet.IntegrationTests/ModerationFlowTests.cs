using System.Net;
using System.Net.Http.Json;
using SahaNet.Application.Market;
using SahaNet.Application.Moderation;
using SahaNet.Modules.Fishing.Application;

namespace SahaNet.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ModerationFlowTests(SahaNetApiFactory factory)
{
    private static object Listing(string title, string? description = null) => new
    {
        categoryKey = "fishing-equipment",
        title,
        description,
        type = "FixedPrice",
        unitPrice = 1500m,
        quantity = 1m,
        attributes = new Dictionary<string, string> { ["kind"] = "ag", ["condition"] = "kullanilmis" },
    };

    [Fact]
    public async Task UserReport_ModeratorRemovesListing_AndSuspendsSeller()
    {
        var (seller, sellerAuth) = await factory.LoginOnboardedAsync("Şüpheli");
        var (reporter, _) = await factory.LoginOnboardedAsync("Dikkatli");
        var listing = (await (await seller.PostAsJsonAsync("/api/market/listings", Listing("Sahte ağ ilanı"))).Content.ReadFromJsonAsync<ListingResponse>())!;

        var report = await reporter.PostAsJsonAsync("/api/reports", new { targetType = "Listing", targetId = listing.Id, reason = "Fraud", note = "Para istedi, ürün yok" });
        report.StatusCode.Should().Be(HttpStatusCode.OK);
        (await reporter.PostAsJsonAsync("/api/reports", new { targetType = "Listing", targetId = listing.Id, reason = "Fraud" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "aynı içerik iki kez şikâyet edilemez");
        (await reporter.GetAsync(new Uri("/api/admin/reports", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var admin = await factory.GetAdminClientAsync();
        var queue = await admin.GetFromJsonAsync<ReportPage>("/api/admin/reports?status=Open&pageSize=100");
        var item = queue!.Items.Single(r => r.TargetId == listing.Id);
        item.TargetPreview.Should().Contain("Sahte ağ ilanı");
        item.TargetOwnerId.Should().Be(sellerAuth.UserId);

        (await admin.PostAsJsonAsync($"/api/admin/reports/{item.Id}/resolve", new { action = "SuspendUser", note = "Dolandırıcılık" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await factory.CreateClient().GetFromJsonAsync<ListingResponse>($"/api/market/listings/{listing.Id}");
        after!.Status.Should().Be("Removed");

        // Askıdaki kullanıcı oturum yenileyemez ve yeni ilan veremez.
        (await seller.PostAsJsonAsync("/api/auth/token/refresh", new { refreshToken = sellerAuth.RefreshToken }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await seller.PostAsJsonAsync("/api/market/listings", Listing("Yeni ilan"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var users = await admin.GetFromJsonAsync<List<AdminUserResponse>>("/api/admin/users?status=Suspended");
        users.Should().Contain(u => u.Id == sellerAuth.UserId && u.PhoneMasked.Contains('*'), "yönetici görünümünde telefon maskelidir");

        (await admin.PostAsync(new Uri($"/api/admin/users/{sellerAuth.UserId}/unsuspend", UriKind.Relative), null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var audit = await admin.GetFromJsonAsync<List<AuditResponse>>("/api/admin/audit");
        audit.Should().Contain(a => a.Action == "report.SuspendUser" && a.TargetId == listing.Id.ToString());
        audit.Should().Contain(a => a.Action == "user.unsuspend");
    }

    [Fact]
    public async Task SpamFilter_BlocksObviousSpam_AndQueuesBorderline()
    {
        var (seller, _) = await factory.LoginOnboardedAsync();

        var blocked = await seller.PostAsJsonAsync("/api/market/listings", Listing("Bahis ve kumar kazanç", "http://kazan.xyz hemen tıkla"));
        blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var borderline = await seller.PostAsJsonAsync("/api/market/listings", Listing("Uzatma ağı", "Detaylar www.agpazari.com adresinde"));
        borderline.StatusCode.Should().Be(HttpStatusCode.OK, "orta puan yayınlanır ama kuyruğa düşer");
        var listing = (await borderline.Content.ReadFromJsonAsync<ListingResponse>())!;

        var admin = await factory.GetAdminClientAsync();
        var queue = await admin.GetFromJsonAsync<ReportPage>("/api/admin/reports?status=Open&pageSize=100");
        queue!.Items.Should().Contain(r => r.TargetId == listing.Id && r.BySystem && r.SpamScore >= 3);
    }

    [Fact]
    public async Task Dashboard_IncludesCoreAndModuleMetrics()
    {
        var admin = await factory.GetAdminClientAsync();

        var dashboard = await admin.GetFromJsonAsync<DashboardResponse>("/api/admin/dashboard");

        dashboard!.Core.TotalUsers.Should().BeGreaterThan(0);
        dashboard.Core.SignupsLast30Days.Should().HaveCount(30);
        dashboard.Modules.Should().ContainKey("fishing").WhoseValue.Should().ContainKey("bans");
    }

    [Fact]
    public async Task Admin_UpdatesFishingRules_AndAssignsModerator()
    {
        var admin = await factory.GetAdminClientAsync();
        var (target, targetAuth) = await factory.LoginAsync();

        var created = await admin.PostAsJsonAsync("/api/fishing/admin/bans", new { title = "Test yasağı", startMonth = 12, startDay = 1, endMonth = 1, endDay = 31, speciesCode = "mezgit" });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PutAsJsonAsync("/api/fishing/admin/species/levrek", new { minLengthCm = 26 })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await target.PostAsJsonAsync("/api/fishing/admin/bans", new { title = "x", startMonth = 1, startDay = 1, endMonth = 2, endDay = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var species = await factory.CreateClient().GetFromJsonAsync<List<SpeciesResponse>>("/api/fishing/species");
        species!.Single(s => s.Code == "levrek").MinLengthCm.Should().Be(26);

        (await admin.PutAsJsonAsync($"/api/admin/users/{targetAuth.UserId}/role", new { role = "Moderator" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AdminPanel_IsServedWithStrictCsp()
    {
        var response = await factory.CreateClient().GetAsync(new Uri("/admin/index.html", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("script-src 'self'").And.NotContain("unsafe-inline");
    }
}
