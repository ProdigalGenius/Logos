using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace SahaNet.Application.Moderation;

public enum SpamContent
{
    Listing = 0,
    Message = 1,
}

public enum SpamVerdict
{
    Allow = 0,

    /// <summary>Yayınlanır ama moderatör kuyruğuna düşer.</summary>
    Review = 1,

    /// <summary>Reddedilir.</summary>
    Block = 2,
}

public sealed record SpamResult(SpamVerdict Verdict, int Score, IReadOnlyList<string> Reasons);

public sealed class SpamOptions
{
    public const string SectionName = "Spam";

    public IReadOnlyList<string> BlockedWords { get; init; } = ["kumar", "bahis", "casino", "kripto kazanç", "hızlı para"];

    public int ReviewThreshold { get; init; } = 3;

    public int BlockThreshold { get; init; } = 6;
}

public interface ISpamFilter
{
    SpamResult Evaluate(SpamContent content, string? text, IReadOnlyCollection<string> recentTexts);
}

/// <summary>
/// Kural tabanlı, açıklanabilir spam puanı. Telefon numarası hedef kitlede olağan olduğu için cezalandırılmaz;
/// dış linkler (WhatsApp paylaşımı hariç), yasaklı kelimeler, bağırma ve aynı ilanın tekrarı puan alır.
/// </summary>
public sealed partial class RuleBasedSpamFilter(IOptions<SpamOptions> options) : ISpamFilter
{
    public SpamResult Evaluate(SpamContent content, string? text, IReadOnlyCollection<string> recentTexts)
    {
        ArgumentNullException.ThrowIfNull(recentTexts);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new SpamResult(SpamVerdict.Allow, 0, []);
        }

        var settings = options.Value;
        var reasons = new List<string>();
        var score = 0;
        var lower = text.ToLowerInvariant();

        foreach (var word in settings.BlockedWords.Where(w => lower.Contains(w.ToLowerInvariant(), StringComparison.Ordinal)))
        {
            score += 5;
            reasons.Add($"yasaklı ifade: {word}");
        }

        var links = LinkPattern().Matches(text).Count(m => !m.Value.Contains("wa.me", StringComparison.OrdinalIgnoreCase));
        if (links > 0)
        {
            score += 3 * links;
            reasons.Add($"{links} dış link");
        }

        var letters = text.Where(char.IsLetter).ToList();
        if (letters.Count >= 15 && letters.Count(char.IsUpper) > letters.Count * 0.7)
        {
            score += 2;
            reasons.Add("büyük harfle bağırma");
        }

        if (RepeatPattern().IsMatch(text))
        {
            score += 1;
            reasons.Add("tekrarlanan karakter");
        }

        var firstLine = text.Split('\n')[0].Trim();
        if (content == SpamContent.Listing && recentTexts.Any(t => string.Equals(t.Trim(), firstLine, StringComparison.OrdinalIgnoreCase)))
        {
            score += 4;
            reasons.Add("son 24 saatte aynı ilan");
        }

        var verdict = score >= settings.BlockThreshold ? SpamVerdict.Block
            : score >= settings.ReviewThreshold ? SpamVerdict.Review
            : SpamVerdict.Allow;
        return new SpamResult(verdict, score, reasons);
    }

    [GeneratedRegex(@"(https?://|www\.)\S+|\b\S+\.(com|net|org|xyz|ru|io|ly)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"(.)\1{5,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex RepeatPattern();
}
