using System.Net;
using System.Net.Http.Json;

namespace SahaNet.IntegrationTests;

[Collection(ApiCollection.Name)]
public class HealthEndpointTests(SahaNetApiFactory factory)
{
    private sealed record HealthResponse(string Status, IReadOnlyList<HealthEntry> Checks);

    private sealed record HealthEntry(string Name, string Status);

    [Fact]
    public async Task Live_ReturnsHealthy()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ready_ChecksPostgresAndRedis()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Status.Should().Be("Healthy");
        body.Checks.Select(c => c.Name).Should().BeEquivalentTo("postgresql", "redis");
    }
}
