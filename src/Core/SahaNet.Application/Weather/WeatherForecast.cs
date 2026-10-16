using SahaNet.Domain.Geo;

namespace SahaNet.Application.Weather;

/// <summary>Saatlik hava/deniz verisi. Dalga verisi kara noktalarında olmayabilir.</summary>
public sealed record WeatherHour(
    DateTimeOffset Time,
    double? WindSpeedKmh,
    double? WindGustKmh,
    double? WindDirectionDeg,
    double? WaveHeightM,
    double? WaveDirectionDeg,
    double? WavePeriodS);

public sealed record WeatherForecast(GeoPoint Location, DateTimeOffset RetrievedAt, IReadOnlyList<WeatherHour> Hours);

public interface IWeatherProvider
{
    /// <summary>Verilen nokta için saatlik tahmin (en fazla <paramref name="days"/> gün).</summary>
    Task<WeatherForecast> GetForecastAsync(GeoPoint location, int days, CancellationToken cancellationToken);
}
