using SahaNet.Domain.Identity;

namespace SahaNet.Application.Identity.Abstractions;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);

    /// <summary>Kriptografik olarak güvenli, opak yenileme belirteci üretir.</summary>
    string GenerateRefreshToken();
}
