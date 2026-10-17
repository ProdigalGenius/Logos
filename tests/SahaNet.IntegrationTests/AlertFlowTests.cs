using System.Net;
using System.Net.Http.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SahaNet.Application.Alerts;
using SahaNet.Application.Identity.Privacy;
using SahaNet.Application.Retention;

namespace SahaNet.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AlertFlowTests(SahaNetApiFactory factory)
{
    private async Task<RegionResponse> RegionAsync(string code)
    {
        var regions = await factory.CreateClient().GetFromJsonAsync<List<RegionResponse>>("/api/geo/regions");
        return regions!.Single(r => r.Code == code);
    }

    [Fact]
    public async Task Regions_AreSeeded_AndNearbyUsesPostGis()
    {
        var client = factory.CreateClient();

        var regions = await client.GetFromJsonAsync<List<RegionResponse>>("/api/geo/regions");
        var bosphorus = await client.GetFromJsonAsync<List<NearbyRegionResponse>>("/api/geo/regions/nearby?lat=41.10&lon=29.05");
        var ankara = await client.GetFromJsonAsync<List<NearbyRegionResponse>>("/api/geo/regions/nearby?lat=39.93&lon=32.85");
        var invalid = await client.GetAsync(new Uri("/api/geo/regions/nearby?lat=120&lon=29", UriKind.Relative));

        regions.Should().HaveCount(13);
        bosphorus!.First().Region.Code.Should().Be("istanbul-bogazi");
        bosphorus![0].DistanceKm.Should().BeLessThan(5);
        ankara.Should().BeEmpty();
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Subscription_Lifecycle_AndExport()
    {
        var (client, _) = await factory.LoginAsync();
        var region = await RegionAsync("istanbul-bogazi");

        var subscribe = await client.PutAsJsonAsync($"/api/me/subscriptions/{region.Id}", new { topics = new[] { "Weather", "Community" } });
        subscribe.StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/me/devices", new RegisterDeviceCommand("test-fcm-token-" + Guid.NewGuid(), "Android")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var mine = await client.GetFromJsonAsync<List<SubscriptionResponse>>("/api/me/subscriptions");
        mine.Should().ContainSingle().Which.Topics.Should().BeEquivalentTo("Weather", "Community");

        var export = await client.GetFromJsonAsync<PersonalDataExport>("/api/me/export");
        export!.RegionSubscriptions.Should().ContainSingle();
        export.Devices.Should().ContainSingle().Which.Platform.Should().Be("Android");

        (await client.DeleteAsync(new Uri($"/api/me/subscriptions/{region.Id}", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync(new Uri($"/api/me/subscriptions/{region.Id}", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Storm_IsDetected_Dispatched_AndListed()
    {
        var region = await RegionAsync("akdeniz-dogu");
        factory.Weather.MakeStormy(region.Latitude, region.Longitude);

        var (client, _) = await factory.LoginAsync();
        var token = "storm-device-" + Guid.NewGuid();
        await client.PutAsJsonAsync($"/api/me/subscriptions/{region.Id}", new { topics = new[] { "Market" } });
        await client.PostAsJsonAsync("/api/me/devices", new RegisterDeviceCommand(token, "Android"));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CheckWeatherAlertsCommand());
            result.Failures.Should().Be(0);
        }

        factory.Push.Sent.Should().Contain(p => p.Token == token && p.Message.Title.StartsWith("FIRTINA UYARISI", StringComparison.Ordinal),
            "kritik uyarı konu seçiminden bağımsız olarak tüm abonelere gider");

        var active = await client.GetFromJsonAsync<List<AlertResponse>>($"/api/alerts/active?regionId={region.Id}");
        active.Should().ContainSingle().Which.Severity.Should().Be("Critical");

        var forecast = await client.GetFromJsonAsync<ForecastResponse>($"/api/geo/regions/{region.Id}/forecast?days=2");
        forecast!.Hours.Should().HaveCount(48);
        forecast.ActiveAlerts.Should().ContainSingle();
    }

    [Fact]
    public async Task RetentionAndPurgeJobs_RunAgainstRealDatabase()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var retention = await sender.Send(new ApplyRetentionPolicyCommand());
        var purged = await sender.Send(new PurgeDeletedAccountsCommand());

        retention.Should().NotBeNull();
        purged.Should().BeGreaterThanOrEqualTo(0);
    }
}
