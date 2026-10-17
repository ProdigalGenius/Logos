using System.Globalization;
using SahaNet.Application.Options;
using SahaNet.Application.Weather;
using SahaNet.Domain.Alerts;

namespace SahaNet.Application.Alerts;

public sealed record WeatherAlertCandidate(
    AlertSeverity Severity,
    DateTimeOffset From,
    DateTimeOffset To,
    DateTimeOffset PeakAt,
    double? PeakWindKmh,
    double? PeakGustKmh,
    double? PeakWaveM);

/// <summary>Tahmini eşiklerle karşılaştırıp ileriye dönük en ciddi uyarıyı çıkarır. Saf fonksiyon.</summary>
public static class WeatherAlertEvaluator
{
    public static WeatherAlertCandidate? Evaluate(WeatherForecast forecast, DateTimeOffset now, AlertOptions options)
    {
        ArgumentNullException.ThrowIfNull(forecast);
        ArgumentNullException.ThrowIfNull(options);

        var window = forecast.Hours
            .Where(h => h.Time >= now.AddHours(-1) && h.Time <= now + options.Lookahead)
            .Select(h => (Hour: h, Severity: Classify(h, options)))
            .Where(x => x.Severity > AlertSeverity.Info)
            .ToList();

        if (window.Count == 0)
        {
            return null;
        }

        var severity = window.Max(x => x.Severity);
        var hours = window.Where(x => x.Severity == severity).Select(x => x.Hour).OrderBy(h => h.Time).ToList();
        var peak = hours
            .OrderByDescending(h => h.WindGustKmh ?? h.WindSpeedKmh ?? 0)
            .ThenByDescending(h => h.WaveHeightM ?? 0)
            .First();

        return new WeatherAlertCandidate(
            severity,
            hours[0].Time,
            hours[^1].Time.AddHours(1),
            peak.Time,
            hours.Max(h => h.WindSpeedKmh),
            hours.Max(h => h.WindGustKmh),
            hours.Max(h => h.WaveHeightM));
    }

    public static AlertSeverity Classify(WeatherHour hour, AlertOptions options)
    {
        ArgumentNullException.ThrowIfNull(hour);
        ArgumentNullException.ThrowIfNull(options);

        if (hour.WindSpeedKmh >= options.CriticalWindKmh || hour.WindGustKmh >= options.CriticalGustKmh || hour.WaveHeightM >= options.CriticalWaveM)
        {
            return AlertSeverity.Critical;
        }

        if (hour.WindSpeedKmh >= options.WarningWindKmh || hour.WindGustKmh >= options.WarningGustKmh || hour.WaveHeightM >= options.WarningWaveM)
        {
            return AlertSeverity.Warning;
        }

        return AlertSeverity.Info;
    }

    /// <summary>Aynı bölge, seviye ve günde ikinci bir uyarı üretilmez.</summary>
    public static string DedupKey(Guid regionId, WeatherAlertCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return string.Create(CultureInfo.InvariantCulture, $"weather:{regionId:N}:{candidate.Severity}:{candidate.From:yyyyMMdd}");
    }

    public static (string Title, string Message) Compose(string regionName, WeatherAlertCandidate candidate, TimeZoneInfo displayZone)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var culture = CultureInfo.GetCultureInfo("tr-TR");
        var from = TimeZoneInfo.ConvertTime(candidate.From, displayZone);
        var title = candidate.Severity == AlertSeverity.Critical
            ? $"FIRTINA UYARISI — {regionName}"
            : $"Kuvvetli rüzgâr/dalga uyarısı — {regionName}";

        var parts = new List<string>();
        if (candidate.PeakWindKmh is { } wind)
        {
            parts.Add(string.Create(culture, $"rüzgâr {wind:0} km/s"));
        }

        if (candidate.PeakGustKmh is { } gust)
        {
            parts.Add(string.Create(culture, $"hamle {gust:0} km/s"));
        }

        if (candidate.PeakWaveM is { } wave)
        {
            parts.Add(string.Create(culture, $"dalga {wave:0.0} m"));
        }

        var message = string.Create(culture, $"{from:dd MMMM HH:mm} itibarıyla {string.Join(", ", parts)} bekleniyor.");
        if (candidate.Severity == AlertSeverity.Critical)
        {
            message += " Denize çıkmayın, tekneyi emniyete alın.";
        }

        return (title, message);
    }
}
