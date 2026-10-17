using System.Net;
using System.Text;
using SahaNet.Domain.Geo;
using SahaNet.Infrastructure.Options;
using SahaNet.Infrastructure.Weather;
using SahaNet.UnitTests.Fakes;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace SahaNet.UnitTests.Infrastructure;

public class OpenMeteoTests
{
    private const string WindJson = """
        {"hourly":{"time":["2026-10-16T00:00","2026-10-16T01:00"],"wind_speed_10m":[12.5,null],"wind_gusts_10m":[20.1,25.0],"wind_direction_10m":[180,190]}}
        """;

    private const string MarineJson = """
        {"hourly":{"time":["2026-10-16T01:00"],"wave_height":[1.4],"wave_direction":[200],"wave_period":[5.5]}}
        """;

    private sealed class StubHandler(Func<Uri, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(respond(request.RequestUri!));
        }
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Merges_WindAndMarine_ByTime()
    {
        using var handler = new StubHandler(uri => Json(uri.Host.StartsWith("marine", StringComparison.Ordinal) ? MarineJson : WindJson));
        using var http = new HttpClient(handler);
        var provider = new OpenMeteoWeatherProvider(http, MsOptions.Create(new WeatherOptions()), new FakeClock());

        var forecast = await provider.GetForecastAsync(GeoPoint.FromTrusted(41.12, 29.06), 3, CancellationToken.None);

        forecast.Hours.Should().HaveCount(2);
        forecast.Hours[0].Time.Should().Be(new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero));
        forecast.Hours[0].WindSpeedKmh.Should().Be(12.5);
        forecast.Hours[0].WaveHeightM.Should().BeNull();
        forecast.Hours[1].WindSpeedKmh.Should().BeNull();
        forecast.Hours[1].WaveHeightM.Should().Be(1.4);
        handler.Requests.Should().Contain(u => u.Query.Contains("latitude=41.12", StringComparison.Ordinal) && u.Query.Contains("forecast_days=3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MarineUnavailable_StillReturnsWind()
    {
        using var handler = new StubHandler(uri => uri.Host.StartsWith("marine", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.BadRequest)
            : Json(WindJson));
        using var http = new HttpClient(handler);
        var provider = new OpenMeteoWeatherProvider(http, MsOptions.Create(new WeatherOptions()), new FakeClock());

        var forecast = await provider.GetForecastAsync(GeoPoint.FromTrusted(39.9, 32.8), 1, CancellationToken.None);

        forecast.Hours.Should().HaveCount(2).And.OnlyContain(h => h.WaveHeightM == null);
    }
}
