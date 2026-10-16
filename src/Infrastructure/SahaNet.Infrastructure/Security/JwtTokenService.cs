using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Domain.Identity;
using SahaNet.Infrastructure.Options;

namespace SahaNet.Infrastructure.Security;

internal sealed class JwtTokenService(IOptions<JwtOptions> options, IDateTimeProvider clock) : ITokenService
{
    public const string NicheClaim = "niche";

    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken CreateAccessToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var settings = options.Value;
        var now = clock.UtcNow;
        var expires = now + settings.AccessTokenLifetime;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (user.NicheKey is not null)
        {
            claims.Add(new Claim(NicheClaim, user.NicheKey));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(CreateSigningKey(settings.SigningKey), SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(_handler.CreateToken(descriptor), expires);
    }

    public string GenerateRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48));

    public static SymmetricSecurityKey CreateSigningKey(string signingKey) => new(Encoding.UTF8.GetBytes(signingKey));
}
