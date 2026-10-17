using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SahaNet.Application.Consents;
using SahaNet.Application.Identity.Privacy;
using SahaNet.Infrastructure.Persistence;
using SahaNet.Modules.Fishing.Application;

namespace SahaNet.IntegrationTests;

[Collection(ApiCollection.Name)]
public class FishingFlowTests(SahaNetApiFactory factory)
{
    private static async Task SetLocationConsentAsync(HttpClient client, bool granted)
    {
        var documents = await client.GetFromJsonAsync<List<ConsentDocumentResponse>>("/api/consents/documents");
        var location = documents!.Single(d => d.Type == "LocationSharing");
        (await client.PostAsJsonAsync("/api/me/consents", new RecordConsentsCommand([new ConsentDecision(location.Id, granted)], "mobile")))
            .EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Catalog_PortsSpeciesAndBans_ArePublic()
    {
        var client = factory.CreateClient();

        var ports = await client.GetFromJsonAsync<List<PortResponse>>("/api/fishing/ports?lat=41.17&lon=29.06");
        var species = await client.GetFromJsonAsync<List<SpeciesResponse>>("/api/fishing/species");
        var bans = await client.GetFromJsonAsync<List<BanResponse>>("/api/fishing/bans?species=kalkan");

        ports![0].Code.Should().Be("sariyer");
        species.Should().Contain(s => s.Code == "levrek" && s.MinLengthCm == 25);
        species![^1].Code.Should().Be("diger");
        bans.Should().Contain(b => b.SpeciesCode == "kalkan");
    }

    [Fact]
    public async Task SeaStatus_ForPort_UsesRegionAndForecast()
    {
        var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<SeaStatusResponse>("/api/fishing/ports/sariyer/sea-status?days=2");
        var missing = await client.GetAsync(new Uri("/api/fishing/ports/yok/sea-status", UriKind.Relative));

        status!.RegionCodes.Should().Contain("istanbul-bogazi");
        status.Days.Should().NotBeEmpty();
        status.Hours.Should().NotBeEmpty().And.OnlyContain(h => h.Beaufort != null);
        status.Now.Should().Be(SeaVerdict.Go);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Spots_AreEncryptedAtRest_AndSharingNeedsConsent()
    {
        var (owner, _) = await factory.LoginAsync();
        var friendPhone = SahaNetApiFactory.NewPhone();
        var (friend, friendAuth) = await factory.LoginAsync(friendPhone);

        var created = await owner.PostAsJsonAsync("/api/fishing/spots", new
        {
            name = "Gizli taşlık",
            latitude = 41.123456,
            longitude = 29.054321,
            depthM = 18.5,
            notes = "Sabah erken",
            species = new[] { "levrek", "lufer" },
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var spot = (await created.Content.ReadFromJsonAsync<SpotResponse>())!;
        spot.Species.Should().Equal("levrek", "lufer");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SahaNetDbContext>();
            var raw = await db.Database
                .SqlQuery<string>($"SELECT location_encrypted AS \"Value\" FROM fishing.spots WHERE \"Id\" = {spot.Id}")
                .SingleAsync();
            raw.Should().StartWith("v1:").And.NotContain("41.12");
        }

        var noConsent = await owner.PostAsJsonAsync($"/api/fishing/spots/{spot.Id}/shares", new { phone = friendPhone });
        noConsent.StatusCode.Should().Be(HttpStatusCode.Forbidden, "konum paylaşımı varsayılan kapalı, açık rıza gerekir");

        await SetLocationConsentAsync(owner, granted: true);
        (await owner.PostAsJsonAsync($"/api/fishing/spots/{spot.Id}/shares", new { phone = friendPhone }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var friendSpots = await friend.GetFromJsonAsync<List<SpotResponse>>("/api/fishing/spots");
        friendSpots.Should().ContainSingle(s => s.Id == spot.Id && !s.IsMine && s.Notes == null, "notlar yalnızca sahibine görünür");

        await SetLocationConsentAsync(owner, granted: false);
        (await friend.GetFromJsonAsync<List<SpotResponse>>("/api/fishing/spots"))
            .Should().NotContain(s => s.Id == spot.Id, "rıza geri çekilince paylaşımlar kaldırılır");

        (await friend.PutAsJsonAsync($"/api/fishing/spots/{spot.Id}", new { name = "x", latitude = 1.0, longitude = 1.0 }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "başkasının noktası düzenlenemez");
        friendAuth.UserId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CatchLog_WarnsAndComputesSeasonStats()
    {
        var (client, _) = await factory.LoginAsync();
        var caughtAt = DateTimeOffset.UtcNow.AddHours(-3);

        var small = await client.PostAsJsonAsync("/api/fishing/catches", new { caughtAt, speciesCode = "levrek", weightKg = 1.2, count = 3, lengthCm = 22.0 });
        var big = await client.PostAsJsonAsync("/api/fishing/catches", new { caughtAt, speciesCode = "lufer", weightKg = 2.3 });
        var unknown = await client.PostAsJsonAsync("/api/fishing/catches", new { caughtAt, speciesCode = "balina", weightKg = 1.0 });
        var invalid = await client.PostAsJsonAsync("/api/fishing/catches", new { caughtAt, speciesCode = "levrek", weightKg = -1.0 });

        small.StatusCode.Should().Be(HttpStatusCode.OK);
        (await small.Content.ReadFromJsonAsync<RecordCatchResponse>())!.Warnings.Should().ContainSingle(w => w.Contains("25 cm"));
        big.StatusCode.Should().Be(HttpStatusCode.OK);
        unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var stats = await client.GetFromJsonAsync<SeasonStatsResponse>("/api/fishing/catches/stats");
        stats!.TotalWeightKg.Should().Be(3.5);
        stats.Entries.Should().Be(2);

        var export = await client.GetFromJsonAsync<PersonalDataExport>("/api/me/export");
        export!.Modules.Should().ContainKey("fishing");
    }
}
