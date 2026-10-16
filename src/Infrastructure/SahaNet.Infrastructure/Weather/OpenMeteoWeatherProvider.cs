using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Weather;
using SahaNet.Domain.Geo;
using SahaNet.Infrastructure.Options;

namespace SahaNet.Infrastructure.Weather;

/// <summary>
/// Open-Meteo hava (rüzgâr) ve deniz (dalga) API'lerini birleştirir. Saatler UTC'dir.
/// Not: Ücretsiz uç nokta ticari olmayan kullanım içindir; üretimde API anahtarı tanımlanmalıdır.
/// </summary>
internal sealed class OpenMeteoWeatherProvider(HttpClient http, IOptions<WeatherOptions> options, IDateTimeProvider clock) : IWeatherProvider
{
    public async Task<WeatherForecast> GetForecastAsync(GeoPoint location, int days, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(location);
        days = Math.Clamp(days, 1, 7);
        var settings = options.Value;

        var forecastUrl = BuildUrl(settings.ForecastBaseUrl, "v1/forecast", location, days, "wind_speed_10m,wind_gusts_10m,wind_direction_10m", settings.ApiKey)
            + "&wind_speed_unit=kmh";
        var marineUrl = BuildUrl(settings.MarineBaseUrl, "v1/marine", location, days, "wave_height,wave_direction,wave_period", settings.ApiKey);

        var windTask = http.GetFromJsonAsync<OpenMeteoResponse>(forecastUrl, cancellationToken);
        var marineTask = GetMarineOrNullAsync(marineUrl, cancellationToken);
        await Task.WhenAll(windTask, marineTask);

        var wind = (await windTask)?.Hourly ?? throw new HttpRequestException("Open-Meteo boş yanıt döndürdü.");
        var marine = (await marineTask)?.Hourly;

        return new WeatherForecast(location, clock.UtcNow, Merge(wind, marine));
    }

    internal static IReadOnlyList<WeatherHour> Merge(OpenMeteoHourly wind, OpenMeteoHourly? marine)
    {
        var waveByTime = new Dictionary<string, int>(StringComparer.Ordinal);
        if (marine?.Time is { } marineTimes)
        {
            for (var i = 0; i < marineTimes.Count; i++)
            {
                waveByTime[marineTimes[i]] = i;
            }
        }

        var hours = new List<WeatherHour>(wind.Time?.Count ?? 0);
        for (var i = 0; i < (wind.Time?.Count ?? 0); i++)
        {
            var timeText = wind.Time![i];
            var hasWave = waveByTime.TryGetValue(timeText, out var w);
            hours.Add(new WeatherHour(
                ParseUtc(timeText),
                At(wind.WindSpeed10m, i),
                At(wind.WindGusts10m, i),
                At(wind.WindDirection10m, i),
                hasWave ? At(marine!.WaveHeight, w) : null,
                hasWave ? At(marine!.WaveDirection, w) : null,
                hasWave ? At(marine!.WavePeriod, w) : null));
        }

        return hours;
    }

    private async Task<OpenMeteoResponse?> GetMarineOrNullAsync(string url, CancellationToken cancellationToken)
    {
        // Kara noktalarında deniz verisi yoktur (400 döner); rüzgâr verisi yine de kullanılır.
        using var response = await http.GetAsync(new Uri(url), cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<OpenMeteoResponse>(cancellationToken)
            : null;
    }

    private static string BuildUrl(Uri baseUrl, string path, GeoPoint location, int days, string hourly, string? apiKey)
    {
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"{new Uri(baseUrl, path)}?latitude={location.Latitude:0.####}&longitude={location.Longitude:0.####}&hourly={hourly}&forecast_days={days}&timezone=UTC");
        return string.IsNullOrWhiteSpace(apiKey) ? url : url + "&apikey=" + Uri.EscapeDataString(apiKey);
    }

    private static DateTimeOffset ParseUtc(string value) =>
        new(DateTime.SpecifyKind(DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal), DateTimeKind.Utc));

    private static double? At(IReadOnlyList<double?>? values, int index) =>
        values is not null && index < values.Count ? values[index] : null;
}

internal sealed record OpenMeteoResponse([property: JsonPropertyName("hourly")] OpenMeteoHourly? Hourly);

internal sealed record OpenMeteoHourly(
    [property: JsonPropertyName("time")] IReadOnlyList<string>? Time,
    [property: JsonPropertyName("wind_speed_10m")] IReadOnlyList<double?>? WindSpeed10m = null,
    [property: JsonPropertyName("wind_gusts_10m")] IReadOnlyList<double?>? WindGusts10m = null,
    [property: JsonPropertyName("wind_direction_10m")] IReadOnlyList<double?>? WindDirection10m = null,
    [property: JsonPropertyName("wave_height")] IReadOnlyList<double?>? WaveHeight = null,
    [property: JsonPropertyName("wave_direction")] IReadOnlyList<double?>? WaveDirection = null,
    [property: JsonPropertyName("wave_period")] IReadOnlyList<double?>? WavePeriod = null);
