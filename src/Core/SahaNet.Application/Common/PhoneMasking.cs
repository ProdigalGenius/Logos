using System.Text.RegularExpressions;

namespace SahaNet.Application.Common;

/// <summary>KVKK: serbest metin içindeki Türkiye cep telefonu numaralarını maskeler (loglar için).</summary>
public static partial class PhoneMasking
{
    public static string MaskInText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        return PhonePattern().Replace(text, m =>
        {
            var digits = new string(m.Value.Where(char.IsAsciiDigit).ToArray());
            return $"5** *** ** {digits[^2..]}";
        });
    }

    [GeneratedRegex(@"(?<!\d)(?:\+?90[\s-]?|0)?5\d{2}[\s-]?\d{3}[\s-]?\d{2}[\s-]?\d{2}(?!\d)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex PhonePattern();
}
