using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Infrastructure.Security;

namespace SahaNet.Api.Infrastructure;

/// <summary>
/// İstekteki JWT'den kullanıcıyı okur. SignalR hub çağrıları HTTP isteği dışında çalışabildiği için
/// hub, bağlantının kimliğini <see cref="Override"/> ile açıkça atar.
/// </summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? Override { get; set; }

    public bool IsAdmin => accessor.HttpContext?.User.FindFirstValue(JwtTokenClaims.Role) == "Admin";

    public Guid? UserId
    {
        get
        {
            if (Override is { } id)
            {
                return id;
            }

            var sub = accessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return Guid.TryParse(sub, out var parsed) ? parsed : null;
        }
    }
}
