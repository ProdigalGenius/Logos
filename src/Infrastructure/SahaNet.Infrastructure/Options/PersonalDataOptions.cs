using System.ComponentModel.DataAnnotations;

namespace SahaNet.Infrastructure.Options;

/// <summary>
/// KVKK kolon şifreleme anahtarları. Üretimde gizli kasa (Vault / KMS) üzerinden verilmeli,
/// asla depoya yazılmamalı.
/// </summary>
public sealed class PersonalDataOptions
{
    public const string SectionName = "PersonalData";

    /// <summary>AES-256-GCM anahtarı, Base64 (32 bayt).</summary>
    [Required]
    public string EncryptionKey { get; init; } = string.Empty;

    /// <summary>HMAC-SHA256 arama anahtarı, Base64 (en az 32 bayt).</summary>
    [Required]
    public string LookupKey { get; init; } = string.Empty;
}
