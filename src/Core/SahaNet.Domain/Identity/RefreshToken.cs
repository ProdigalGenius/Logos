using SahaNet.Domain.Common;

namespace SahaNet.Domain.Identity;

/// <summary>
/// Yenileme belirteci. Her kullanımda döndürülür (rotation); aynı ailedeki iptal edilmiş
/// bir belirtecin tekrar kullanılması çalınma işaretidir ve tüm aile iptal edilir.
/// </summary>
public sealed class RefreshToken : Entity<Guid>
{
    private RefreshToken(Guid id, Guid userId, string tokenHash, Guid familyId, DateTimeOffset now, TimeSpan lifetime)
        : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        FamilyId = familyId;
        CreatedAt = now;
        ExpiresAt = now + lifetime;
    }

    // EF Core
    private RefreshToken()
        : base(Guid.Empty)
    {
        TokenHash = null!;
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; }

    public Guid FamilyId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedById { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public static RefreshToken Issue(Guid userId, string tokenHash, Guid familyId, DateTimeOffset now, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        return new RefreshToken(Guid.NewGuid(), userId, tokenHash, familyId, now, lifetime);
    }

    public bool IsActive(DateTimeOffset now) => !IsRevoked && now < ExpiresAt;

    public void Revoke(DateTimeOffset now, Guid? replacedById = null)
    {
        if (IsRevoked)
        {
            return;
        }

        RevokedAt = now;
        ReplacedById = replacedById;
    }
}
