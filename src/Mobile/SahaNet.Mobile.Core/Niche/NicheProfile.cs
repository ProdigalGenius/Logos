using System.Text.Json;

namespace SahaNet.Mobile.Core.Niche;

/// <summary>Ana ekranda gösterilen modül kutucuğu.</summary>
public sealed record NicheTile(string Key, string Title, string Icon, string Route);

/// <summary>
/// White-label niş profili (build sırasında seçilir: <c>-p:Niche=fishing</c>). Uygulama adı, renk,
/// varsayılan liman/bölge ve ana ekrandaki modüller buradan gelir.
/// </summary>
public sealed record NicheProfile(
    string Key,
    string AppName,
    string PrimaryColor,
    string DefaultRegionCode,
    string? DefaultPortCode,
    IReadOnlyList<NicheTile> Tiles)
{
    public static NicheProfile Load(Stream json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var profile = JsonSerializer.Deserialize<NicheProfile>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Niş profili okunamadı.");

        if (string.IsNullOrWhiteSpace(profile.Key) || profile.Tiles is not { Count: > 0 })
        {
            throw new InvalidOperationException("Niş profili eksik: anahtar ve en az bir modül gerekli.");
        }

        return profile;
    }
}
