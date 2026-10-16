using Microsoft.Extensions.Options;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Application.Options;
using SahaNet.Domain.Identity;

namespace SahaNet.Application.Identity.Auth;

/// <summary>Erişim + yenileme belirteci çiftini üretir; yenileme belirtecinin yalnızca özeti saklanır.</summary>
internal sealed class TokenIssuer(
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokens,
    IPersonalDataProtector protector,
    IDateTimeProvider clock,
    IOptions<IdentityOptions> options)
{
    public (AuthResponse Response, RefreshToken Stored) Issue(User user, Guid familyId, bool isNewUser)
    {
        var access = tokenService.CreateAccessToken(user);
        var rawRefresh = tokenService.GenerateRefreshToken();
        var stored = RefreshToken.Issue(
            user.Id,
            protector.ComputeLookupHash(rawRefresh),
            familyId,
            clock.UtcNow,
            options.Value.RefreshTokenLifetime);

        refreshTokens.Add(stored);

        var response = new AuthResponse(
            user.Id,
            access.Token,
            access.ExpiresAt,
            rawRefresh,
            stored.ExpiresAt,
            isNewUser,
            !user.IsOnboarded);

        return (response, stored);
    }
}
