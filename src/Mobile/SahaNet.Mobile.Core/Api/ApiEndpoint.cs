namespace SahaNet.Mobile.Core.Api;

/// <summary>Sunucu adresi. Test kurulumlarında kullanıcı ayarlardan değiştirebilir (ör. aynı Wi-Fi'deki bilgisayar).</summary>
public sealed class ApiEndpoint(Uri baseUrl)
{
    private Uri _baseUrl = Normalize(baseUrl);

    public Uri BaseUrl
    {
        get => _baseUrl;
        set => _baseUrl = Normalize(value);
    }

    /// <summary>"192.168.1.20:5080" gibi girişleri tam adrese çevirir; geçersizse false.</summary>
    public static bool TryParse(string? text, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var candidate = text.Trim();
        if (!candidate.Contains("://", StringComparison.Ordinal))
        {
            candidate = "http://" + candidate;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var parsed) || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        uri = Normalize(parsed);
        return true;
    }

    private static Uri Normalize(Uri value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.AbsoluteUri.EndsWith('/') ? value : new Uri(value.AbsoluteUri + "/");
    }
}
