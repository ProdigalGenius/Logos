using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SahaNet.Infrastructure.Security;

/// <summary>JWT imzalama ve doğrulamanın aynı anahtarı kullanmasını sağlar.</summary>
public static class JwtSigningKey
{
    public static SymmetricSecurityKey Create(string signingKey) => new(Encoding.UTF8.GetBytes(signingKey));
}
