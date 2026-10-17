namespace SahaNet.Application.Options;

/// <summary>KVKK veri saklama süreleri. Süresi dolan kayıtlar günlük işle silinir.</summary>
public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>OTP denemeleri (telefon özeti içerir).</summary>
    public TimeSpan OtpChallenges { get; init; } = TimeSpan.FromDays(1);

    /// <summary>Süresi dolmuş veya iptal edilmiş yenileme belirteçleri.</summary>
    public TimeSpan InactiveRefreshTokens { get; init; } = TimeSpan.FromDays(30);

    /// <summary>Uyarı teslim kayıtları.</summary>
    public TimeSpan AlertDeliveries { get; init; } = TimeSpan.FromDays(90);

    /// <summary>Süresi geçmiş uyarılar.</summary>
    public TimeSpan ExpiredAlerts { get; init; } = TimeSpan.FromDays(365);

    /// <summary>90 gün görülmeyen cihaz kayıtları.</summary>
    public TimeSpan StaleDevices { get; init; } = TimeSpan.FromDays(90);
}
