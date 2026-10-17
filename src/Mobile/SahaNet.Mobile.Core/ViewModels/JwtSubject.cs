using System.Text;
using System.Text.Json;

namespace SahaNet.Mobile.Core.ViewModels;

/// <summary>Erişim belirtecindeki kullanıcı kimliğini (sub) okur; doğrulama sunucudadır, burada yalnızca görüntüleme için.</summary>
internal static class JwtSubject
{
    public static Guid? Read(string? jwt) =>
        ReadClaim(jwt, "sub") is { } sub && Guid.TryParse(sub, out var id) ? id : null;

    public static string? ReadRole(string? jwt) => ReadClaim(jwt, "role");

    private static string? ReadClaim(string? jwt, string name)
    {
        var parts = jwt?.Split('.');
        if (parts is not { Length: 3 })
        {
            return null;
        }

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            return doc.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
