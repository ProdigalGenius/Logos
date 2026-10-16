using System.ComponentModel.DataAnnotations;

namespace SahaNet.Infrastructure.Options;

public sealed class WeatherOptions
{
    public const string SectionName = "Weather";

    [Required]
    public Uri ForecastBaseUrl { get; init; } = new("https://api.open-meteo.com/");

    [Required]
    public Uri MarineBaseUrl { get; init; } = new("https://marine-api.open-meteo.com/");

    /// <summary>Ticari kullanım için Open-Meteo API anahtarı (customer-*.open-meteo.com ile birlikte).</summary>
    public string? ApiKey { get; init; }

    public TimeSpan CacheDuration { get; init; } = TimeSpan.FromMinutes(30);
}

public enum PushProvider
{
    Logging = 0,
    Fcm = 1,
}

public sealed class PushOptions
{
    public const string SectionName = "Push";

    public PushProvider Provider { get; init; } = PushProvider.Logging;

    /// <summary>Firebase servis hesabı JSON dosyasının yolu (Provider=Fcm iken zorunlu).</summary>
    public string? FcmCredentialsPath { get; init; }

    /// <summary>Android'de "Rahatsız Etmeyin"i delebilen, yüksek önemli bildirim kanalı.</summary>
    [Required]
    public string AndroidCriticalChannelId { get; init; } = "critical_alerts";

    [Required]
    public string AndroidDefaultChannelId { get; init; } = "alerts";
}

public enum SmsProvider
{
    Logging = 0,
    Netgsm = 1,
}

public sealed class NetgsmOptions
{
    public Uri BaseUrl { get; init; } = new("https://api.netgsm.com.tr/");

    public string? Username { get; init; }

    public string? Password { get; init; }

    /// <summary>BTK onaylı gönderici başlığı.</summary>
    public string? Header { get; init; }
}

public sealed class BackgroundJobOptions
{
    public const string SectionName = "BackgroundJobs";

    /// <summary>false iken Hangfire başlatılmaz; uyarı dağıtımı satır içi çalışır (testler).</summary>
    public bool Enabled { get; init; } = true;

    public bool DashboardEnabled { get; init; }

    [Required]
    public string WeatherCheckCron { get; init; } = "*/30 * * * *";

    [Required]
    public string AccountPurgeCron { get; init; } = "0 3 * * *";

    [Required]
    public string RetentionCron { get; init; } = "30 3 * * *";

    /// <summary>Süresi dolan ilan ve ihaleleri kapatma (dakikalık).</summary>
    [Required]
    public string ListingCloseCron { get; init; } = "* * * * *";

    [Required]
    public string TimeZone { get; init; } = "Europe/Istanbul";
}
