using System.Security.Cryptography;
using System.Text;
using SahaNet.Domain.Common;

namespace SahaNet.Domain.Identity;

public enum OtpVerificationOutcome
{
    Success = 0,
    Invalid = 1,
    Expired = 2,
    Locked = 3,
    AlreadyUsed = 4,
}

/// <summary>
/// Tek kullanımlık SMS kodu denemesi. Kodun kendisi değil, HMAC özeti saklanır.
/// Brute force koruması: belirli sayıda hatalı denemeden sonra kilitlenir.
/// </summary>
public sealed class OtpChallenge : Entity<Guid>
{
    private OtpChallenge(Guid id, string phoneHash, string codeHash, DateTimeOffset now, TimeSpan lifetime)
        : base(id)
    {
        PhoneHash = phoneHash;
        CodeHash = codeHash;
        CreatedAt = now;
        ExpiresAt = now + lifetime;
    }

    // EF Core
    private OtpChallenge()
        : base(Guid.Empty)
    {
        PhoneHash = null!;
        CodeHash = null!;
    }

    public string PhoneHash { get; private set; }

    public string CodeHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public int FailedAttempts { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public static OtpChallenge Create(string phoneHash, string codeHash, DateTimeOffset now, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(codeHash);
        return new OtpChallenge(Guid.NewGuid(), phoneHash, codeHash, now, lifetime);
    }

    public OtpVerificationOutcome Verify(string codeHash, DateTimeOffset now, int maxAttempts)
    {
        ArgumentNullException.ThrowIfNull(codeHash);

        if (ConsumedAt is not null)
        {
            return OtpVerificationOutcome.AlreadyUsed;
        }

        if (FailedAttempts >= maxAttempts)
        {
            return OtpVerificationOutcome.Locked;
        }

        if (now >= ExpiresAt)
        {
            return OtpVerificationOutcome.Expired;
        }

        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(CodeHash),
            Encoding.UTF8.GetBytes(codeHash));

        if (!matches)
        {
            FailedAttempts++;
            return FailedAttempts >= maxAttempts ? OtpVerificationOutcome.Locked : OtpVerificationOutcome.Invalid;
        }

        ConsumedAt = now;
        return OtpVerificationOutcome.Success;
    }
}
