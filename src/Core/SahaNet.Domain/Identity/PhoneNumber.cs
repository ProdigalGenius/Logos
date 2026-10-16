using SahaNet.Domain.Common;

namespace SahaNet.Domain.Identity;

/// <summary>
/// Türkiye cep telefonu numarası, E.164 biçiminde (+905XXXXXXXXX) saklanır.
/// KVKK: <see cref="ToString"/> her zaman maskeli değer döndürür, böylece kazara loglanmaz.
/// </summary>
public sealed class PhoneNumber : ValueObject
{
    private const int NationalLength = 10;

    private PhoneNumber(string value) => Value = value;

    /// <summary>E.164 biçimli tam numara. Yalnızca SMS gönderimi ve şifreleme için kullanılmalı.</summary>
    public string Value { get; }

    public static Result<PhoneNumber> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result.Failure<PhoneNumber>(IdentityErrors.PhoneInvalid);
        }

        var digits = new string(input.Where(char.IsAsciiDigit).ToArray());

        if (digits.Length == NationalLength + 2 && digits.StartsWith("90", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }
        else if (digits.Length == NationalLength + 1 && digits[0] == '0')
        {
            digits = digits[1..];
        }

        if (digits.Length != NationalLength || digits[0] != '5')
        {
            return Result.Failure<PhoneNumber>(IdentityErrors.PhoneInvalid);
        }

        return Result.Success(new PhoneNumber("+90" + digits));
    }

    /// <summary>Kalıcı depodan (şifre çözülmüş) okunan, önceden doğrulanmış değer için.</summary>
    public static PhoneNumber FromTrusted(string e164)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(e164);
        return new PhoneNumber(e164);
    }

    /// <summary>"+90 5** *** ** 67" biçiminde maskeli gösterim.</summary>
    public string Masked => $"+90 {Value[3]}** *** ** {Value[^2..]}";

    public override string ToString() => Masked;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
