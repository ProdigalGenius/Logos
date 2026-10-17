using SahaNet.Application.Alerts;
using SahaNet.Application.Options;
using SahaNet.Application.Weather;
using SahaNet.Domain.Alerts;

namespace SahaNet.Modules.Fishing.Application;

/// <summary>Denize çıkış kararı: ikon ağırlıklı arayüz için üç durumlu.</summary>
public enum SeaVerdict
{
    /// <summary>Yeşil: çıkılabilir.</summary>
    Go = 0,

    /// <summary>Sarı: dikkatli olun.</summary>
    Caution = 1,

    /// <summary>Kırmızı: çıkmayın.</summary>
    NoGo = 2,
}

public sealed record SeaHour(
    DateTimeOffset Time,
    double? WindKmh,
    double? GustKmh,
    int? Beaufort,
    string? WindFrom,
    string? WindName,
    double? WaveM,
    SeaVerdict Verdict);

public sealed record SeaDay(
    DateOnly Date,
    double? MaxWindKmh,
    double? MaxGustKmh,
    int? MaxBeaufort,
    string? DominantWindName,
    double? MaxWaveM,
    SeaVerdict Verdict,
    string Summary);

/// <summary>Saatlik tahmini balıkçının anlayacağı dile çevirir (Beaufort, yerel rüzgâr adları, karar).</summary>
public static class SeaStatusCalculator
{
    private static readonly double[] BeaufortUpperKmh = [1, 6, 12, 20, 29, 39, 50, 62, 75, 89, 103, 118];

    // Türk denizciliğinde 8 ana rüzgâr: kuzeyden saat yönünde.
    private static readonly (string Short, string Name)[] Winds =
    [
        ("K", "Yıldız"), ("KD", "Poyraz"), ("D", "Gündoğusu"), ("GD", "Keşişleme"),
        ("G", "Kıble"), ("GB", "Lodos"), ("B", "Günbatısı"), ("KB", "Karayel"),
    ];

    public static int Beaufort(double kmh)
    {
        for (var i = 0; i < BeaufortUpperKmh.Length; i++)
        {
            if (kmh < BeaufortUpperKmh[i])
            {
                return i;
            }
        }

        return 12;
    }

    /// <summary>Rüzgârın estiği yön (meteorolojik derece, 0 = kuzeyden).</summary>
    public static (string Short, string Name) WindFrom(double degrees)
    {
        var normalized = ((degrees % 360) + 360) % 360;
        return Winds[(int)Math.Round(normalized / 45.0) % 8];
    }

    public static SeaVerdict Verdict(WeatherHour hour, AlertOptions options) =>
        WeatherAlertEvaluator.Classify(hour, options) switch
        {
            AlertSeverity.Critical => SeaVerdict.NoGo,
            AlertSeverity.Warning => SeaVerdict.Caution,
            _ => SeaVerdict.Go,
        };

    public static IReadOnlyList<SeaHour> Hours(IEnumerable<WeatherHour> hours, AlertOptions options) =>
        hours.Select(h =>
        {
            var wind = h.WindDirectionDeg is { } d ? WindFrom(d) : default;
            return new SeaHour(
                h.Time,
                h.WindSpeedKmh,
                h.WindGustKmh,
                h.WindSpeedKmh is { } w ? Beaufort(w) : null,
                h.WindDirectionDeg is null ? null : wind.Short,
                h.WindDirectionDeg is null ? null : wind.Name,
                h.WaveHeightM,
                Verdict(h, options));
        }).ToList();

    public static IReadOnlyList<SeaDay> Days(IEnumerable<WeatherHour> hours, AlertOptions options, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(options);

        return hours
            .GroupBy(h => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(h.Time, zone).DateTime))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var list = g.ToList();
                var maxWind = list.Max(h => h.WindSpeedKmh);
                var verdict = list.Max(h => Verdict(h, options));
                var dominant = DominantDirection(list);
                var windName = dominant is { } deg ? WindFrom(deg).Name : null;
                return new SeaDay(
                    g.Key,
                    maxWind,
                    list.Max(h => h.WindGustKmh),
                    maxWind is { } w ? Beaufort(w) : null,
                    windName,
                    list.Max(h => h.WaveHeightM),
                    verdict,
                    Summarize(verdict, windName, maxWind is { } mw ? Beaufort(mw) : null));
            })
            .ToList();
    }

    /// <summary>Rüzgâr hızıyla ağırlıklı dairesel ortalama yön.</summary>
    internal static double? DominantDirection(IReadOnlyList<WeatherHour> hours)
    {
        double x = 0, y = 0;
        foreach (var h in hours)
        {
            if (h.WindDirectionDeg is { } d)
            {
                var weight = h.WindSpeedKmh ?? 1;
                x += weight * Math.Cos(d * Math.PI / 180);
                y += weight * Math.Sin(d * Math.PI / 180);
            }
        }

        if (x == 0 && y == 0)
        {
            return null;
        }

        return ((Math.Atan2(y, x) * 180 / Math.PI) + 360) % 360;
    }

    private static string Summarize(SeaVerdict verdict, string? windName, int? beaufort)
    {
        var wind = windName is null || beaufort is null ? string.Empty : $"{windName} {beaufort} kuvvetinde. ";
        return verdict switch
        {
            SeaVerdict.NoGo => wind + "Fırtına riski, denize çıkmayın.",
            SeaVerdict.Caution => wind + "Deniz kabarık, dikkatli olun.",
            _ => wind + "Deniz uygun.",
        };
    }
}
