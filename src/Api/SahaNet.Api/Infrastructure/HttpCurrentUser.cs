using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using SahaNet.Application.Identity.Abstractions;

namespace SahaNet.Api.Infrastructure;

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            var sub = principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }
}
