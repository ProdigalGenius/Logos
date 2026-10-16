using System.Net;
using System.Net.Http.Json;
using SahaNet.Modules.Fishing;

namespace SahaNet.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ModuleEndpointTests(SahaNetApiFactory factory)
{
    private sealed record ModuleEntry(string Key, string DisplayName);

    [Fact]
    public async Task Modules_ListsFishing()
    {
        using var client = factory.CreateClient();

        var modules = await client.GetFromJsonAsync<List<ModuleEntry>>(new Uri("/api/modules", UriKind.Relative));

        modules.Should().ContainSingle(m => m.Key == "fishing");
    }

    [Fact]
    public async Task FishingInfo_ReturnsOk()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/fishing/info", UriKind.Relative));
        var body = await response.Content.ReadFromJsonAsync<ModuleInfoResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.DisplayName.Should().Be("BalıkçıNet");
    }
}
