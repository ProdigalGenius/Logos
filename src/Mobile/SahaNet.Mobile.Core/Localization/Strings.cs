using System.Globalization;
using System.Resources;

namespace SahaNet.Mobile.Core.Localization;

/// <summary>Arayüz metinleri. Türkçe varsayılan (Strings.resx), diğer diller uydu kaynaklarla (Strings.en.resx).</summary>
public static class Strings
{
    private static readonly ResourceManager Resources = new("SahaNet.Mobile.Core.Localization.Strings", typeof(Strings).Assembly);

    public static CultureInfo? Culture { get; set; }

    public static string Get(string key) => Get(key, Culture);

    public static string Get(string key, CultureInfo? culture) => Resources.GetString(key, culture) ?? key;

    public static string Format(string key, params object[] args) =>
        string.Format(Culture ?? CultureInfo.CurrentUICulture, Get(key), args);
}
