using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SahaNet.Application.Identity.Privacy;
using SahaNet.Application.Market;
using SahaNet.Application.Moderation;
using SahaNet.Infrastructure.Persistence;
using SahaNet.Modules.Scrap.Application;

namespace SahaNet.IntegrationTests;

/// <summary>Faz 8: ikinci niş yalnızca yeni bir modülle; çekirdek akışları (kurulum, pazar, KVKK, yönetim) değişmeden çalışır.</summary>
[Collection(ApiCollection.Name)]
public class ScrapNicheTests(SahaNetApiFactory factory)
{
    private async Task<(HttpClient Client, Guid UserId)> ScrapUserAsync(string name)
    {
        var (client, auth) = await factory.LoginAsync();
        var documents = await client.GetFromJsonAsync<List<SahaNet.Application.Consents.ConsentDocumentResponse>>("/api/consents/documents");
        await client.PostAsJsonAsync("/api/me/consents", new SahaNet.Application.Consents.RecordConsentsCommand(
            documents!.Where(d => d.IsRequired).Select(d => new SahaNet.Application.Consents.ConsentDecision(d.Id, true)).ToList(), "mobile"));
        (await client.PostAsJsonAsync("/api/me/onboarding", new { nicheKey = "scrap", regionCode = "34", displayName = name }))
            .StatusCode.Should().Be(HttpStatusCode.OK, "çekirdek kurulum yeni nişi modül kaydından tanır");
        return (client, auth.UserId);
    }

    [Fact]
    public async Task BothNiches_AreRegistered_InCoreCatalogs()
    {
        var client = factory.CreateClient();

        var modules = await client.GetFromJsonAsync<List<ModuleEntry>>("/api/modules");
        var categories = await client.GetFromJsonAsync<List<CategoryResponse>>("/api/market/categories?niche=scrap");

        modules!.Select(m => m.Key).Should().Contain(new[] { "fishing", "scrap" });
        categories!.Select(c => c.Key).Should().BeEquivalentTo("scrap-lot", "scrap-equipment");
    }

    [Fact]
    public async Task Pickup_Flow_HidesAddressUntilClaimed()
    {
        var (household, householdId) = await ScrapUserAsync("Ev sahibi");
        var (collector, _) = await ScrapUserAsync("Hurdacı Ahmet");
        var (rival, _) = await ScrapUserAsync("Hurdacı Mehmet");

        var created = await household.PostAsJsonAsync("/api/scrap/pickups", new
        {
            provinceCode = "34",
            district = "Üsküdar",
            address = "Gizli Sok. No:7 Daire 2",
            materials = new[] { "demir", "aku" },
            estimatedKg = 80m,
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var pickup = (await created.Content.ReadFromJsonAsync<PickupResponse>())!;
        pickup.Address.Should().Be("Gizli Sok. No:7 Daire 2");

        var open = await collector.GetFromJsonAsync<List<PickupResponse>>("/api/scrap/pickups/open?province=34");
        var seen = open!.Single(p => p.Id == pickup.Id);
        seen.Address.Should().BeNull("üstlenilmeden adres gösterilmez");
        seen.District.Should().Be("Üsküdar");

        var claimed = await collector.PostAsync(new Uri($"/api/scrap/pickups/{pickup.Id}/claim", UriKind.Relative), null);
        claimed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await claimed.Content.ReadFromJsonAsync<PickupResponse>())!.Address.Should().Be("Gizli Sok. No:7 Daire 2");

        (await rival.PostAsync(new Uri($"/api/scrap/pickups/{pickup.Id}/claim", UriKind.Relative), null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await collector.PostAsync(new Uri($"/api/scrap/pickups/{pickup.Id}/complete", UriKind.Relative), null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SahaNetDbContext>();
            var raw = await db.Database.SqlQuery<string>($"SELECT address_encrypted AS \"Value\" FROM scrap.pickups WHERE \"Id\" = {pickup.Id}").SingleAsync();
            raw.Should().StartWith("v1:").And.NotContain("Gizli");
        }

        var export = await household.GetFromJsonAsync<PersonalDataExport>("/api/me/export");
        export!.Modules.Should().ContainKey("scrap").And.ContainKey("fishing");
        householdId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ScrapLot_UsesCoreMarket_AndPricesUseMedian()
    {
        var (seller, _) = await ScrapUserAsync("Atölye");
        var listing = await seller.PostAsJsonAsync("/api/market/listings", new
        {
            categoryKey = "scrap-lot",
            title = "2 ton talaş demir",
            type = "Auction",
            unitPrice = 8m,
            quantity = 2000m,
            attributes = new Dictionary<string, string> { ["material"] = "demir", ["province"] = "41", ["district"] = "Gebze" },
            minBidIncrement = 0.25m,
        });
        listing.StatusCode.Should().Be(HttpStatusCode.OK, "çekirdek ihale, modülün kategori şemasıyla çalışır");

        foreach (var price in new[] { 9.5m, 10m, 40m })
        {
            var (reporter, _) = await factory.LoginAsync();
            (await reporter.PostAsJsonAsync("/api/scrap/prices", new { materialCode = "bakir", provinceCode = "06", pricePerKg = price }))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var prices = await factory.CreateClient().GetFromJsonAsync<List<ScrapDailyPrice>>("/api/scrap/prices?material=bakir&province=06");
        prices.Should().ContainSingle().Which.Median.Should().Be(10m);
    }

    [Fact]
    public async Task AdminDashboard_IncludesBothModules()
    {
        var admin = await factory.GetAdminClientAsync();

        var dashboard = await admin.GetFromJsonAsync<DashboardResponse>("/api/admin/dashboard");

        dashboard!.Modules.Keys.Should().Contain(new[] { "fishing", "scrap" });
        dashboard.Modules["scrap"].Should().ContainKey("openPickups");
    }

    private sealed record ModuleEntry(string Key, string DisplayName);
}
