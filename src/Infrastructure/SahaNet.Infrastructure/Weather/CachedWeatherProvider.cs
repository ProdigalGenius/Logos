using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using SahaNet.Application.Weather;
using SahaNet.Domain.Geo;
using SahaNet.Infrastructure.Options;

namespace SahaNet.Infrastructure.Weather;

/// <summary>Tahminleri Redis'te önbelleğe alır; aynı bölge için dış API'ye tekrar tekrar gidilmez.</summary>
internal sealed class CachedWeatherProvider(OpenMeteoWeatherProvider inner, IDistributedCache cache, IOptions<WeatherOptions> options) : IWeatherProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<WeatherForecast> GetForecastAsync(GeoPoint location, int days, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(location);

        var key = string.Create(CultureInfo.InvariantCulture, $"weather:v1:{location.Latitude:0.###}:{location.Longitude:0.###}:{days}");
        var cached = await cache.GetStringAsync(key, cancellationToken);
        if (cached is not null && JsonSerializer.Deserialize<CachedForecast>(cached, Json) is { } hit)
        {
            return new WeatherForecast(location, hit.RetrievedAt, hit.Hours);
        }

        var forecast = await inner.GetForecastAsync(location, days, cancellationToken);
        await cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(new CachedForecast(forecast.RetrievedAt, forecast.Hours), Json),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = options.Value.CacheDuration },
            cancellationToken);
        return forecast;
    }

    private sealed record CachedForecast(DateTimeOffset RetrievedAt, IReadOnlyList<WeatherHour> Hours);
}
