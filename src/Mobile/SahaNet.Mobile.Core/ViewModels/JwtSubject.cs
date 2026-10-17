using System.Text;
using System.Text.Json;

namespace SahaNet.Mobile.Core.ViewModels;

/// <summary>Erişim belirtecindeki kullanıcı kimliğini (sub) okur; doğrulama sunucudadır, burada yalnızca görüntüleme için.</summary>
internal static class JwtSubject
{
    public static Guid? Read(string? jwt)
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
            return doc.RootElement.TryGetProperty("sub", out var sub) && Guid.TryParse(sub.GetString(), out var id) ? id : null;
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
