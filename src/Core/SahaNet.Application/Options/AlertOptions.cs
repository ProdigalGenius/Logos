using System.ComponentModel.DataAnnotations;

namespace SahaNet.Application.Options;

/// <summary>Uyarı eşikleri (Beaufort ölçeğine göre varsayılanlar) ve dağıtım ayarları.</summary>
public sealed class AlertOptions
{
    public const string SectionName = "Alerts";

    /// <summary>Uyarı: Beaufort 6 (kuvvetli rüzgâr) ≈ 39 km/s.</summary>
    [Range(1, 300)]
    public double WarningWindKmh { get; init; } = 39;

    [Range(1, 300)]
    public double WarningGustKmh { get; init; } = 50;

    [Range(0.1, 30)]
    public double WarningWaveM { get; init; } = 2.0;

    /// <summary>Kritik: Beaufort 8 (fırtına) ≈ 62 km/s.</summary>
    [Range(1, 300)]
    public double CriticalWindKmh { get; init; } = 62;

    [Range(1, 300)]
    public double CriticalGustKmh { get; init; } = 75;

    [Range(0.1, 30)]
    public double CriticalWaveM { get; init; } = 3.5;

    /// <summary>Uyarı üretmek için ileriye bakılan süre.</summary>
    public TimeSpan Lookahead { get; init; } = TimeSpan.FromHours(24);

    /// <summary>Kritik uyarı push ile ulaşmazsa SMS gönderilsin mi.</summary>
    public bool SmsFallbackForCritical { get; init; } = true;

    /// <summary>Uyarı mesajlarındaki saatler için yerel saat dilimi.</summary>
    [Required]
    public string DisplayTimeZone { get; init; } = "Europe/Istanbul";
}
