using System.ComponentModel.DataAnnotations;

namespace SahaNet.Application.Options;

/// <summary>Kimlik doğrulama ve KVKK süreleri. Sihirli sayı yerine konfigürasyondan gelir.</summary>
public sealed class IdentityOptions
{
    public const string SectionName = "Identity";

    [Range(4, 8)]
    public int OtpLength { get; init; } = 6;

    public TimeSpan OtpLifetime { get; init; } = TimeSpan.FromMinutes(3);

    [Range(1, 10)]
    public int OtpMaxAttempts { get; init; } = 5;

    public TimeSpan OtpResendCooldown { get; init; } = TimeSpan.FromSeconds(60);

    [Range(1, 50)]
    public int OtpMaxPerHour { get; init; } = 5;

    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(30);

    /// <summary>Hesap silme talebinden kalıcı silmeye kadar geçen süre (KVKK: 30 gün).</summary>
    public TimeSpan AccountDeletionGracePeriod { get; init; } = TimeSpan.FromDays(30);

    /// <summary>OTP SMS'inde görünen marka adı (niş bazlı white-label).</summary>
    [Required]
    public string SmsBrandName { get; init; } = "SahaNet";
}
