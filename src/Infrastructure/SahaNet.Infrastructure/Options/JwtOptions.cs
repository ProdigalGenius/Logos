using System.ComponentModel.DataAnnotations;

namespace SahaNet.Infrastructure.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    /// <summary>HMAC-SHA256 imza anahtarı (en az 32 karakter).</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);
}
