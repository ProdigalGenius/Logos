using SahaNet.Domain.Common;

namespace SahaNet.Domain.Identity;

/// <summary>Platform kullanıcısı. Kimlik telefon numarasıdır, e-posta zorunlu değildir.</summary>
public sealed class User : AggregateRoot<Guid>
{
    public const int DisplayNameMaxLength = 50;

    private User(Guid id, PhoneNumber phone, string phoneHash, DateTimeOffset now)
        : base(id)
    {
        Phone = phone;
        PhoneHash = phoneHash;
        CreatedAt = now;
        Status = UserStatus.Active;
    }

    // EF Core
    private User()
        : base(Guid.Empty)
    {
        Phone = null!;
        PhoneHash = null!;
    }

    /// <summary>Şifreli kolonda saklanır.</summary>
    public PhoneNumber Phone { get; private set; }

    /// <summary>Telefonla arama yapmak için HMAC özeti (blind index).</summary>
    public string PhoneHash { get; private set; }

    public string? DisplayName { get; private set; }

    public string? NicheKey { get; private set; }

    public string? RegionCode { get; private set; }

    public UserStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public DateTimeOffset? DeletionRequestedAt { get; private set; }

    public bool IsOnboarded => NicheKey is not null && RegionCode is not null;

    public static User Register(PhoneNumber phone, string phoneHash, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(phone);
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneHash);

        var user = new User(Guid.NewGuid(), phone, phoneHash, now);
        user.Raise(new UserRegistered(user.Id));
        return user;
    }

    public void RecordLogin(DateTimeOffset now) => LastLoginAt = now;

    public Result CompleteOnboarding(string nicheKey, string regionCode, string? displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nicheKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(regionCode);

        var nameResult = SetDisplayName(displayName);
        if (nameResult.IsFailure)
        {
            return nameResult;
        }

        NicheKey = nicheKey;
        RegionCode = regionCode;
        Raise(new UserOnboarded(Id, nicheKey, regionCode));
        return Result.Success();
    }

    public Result UpdateProfile(string? displayName, string? regionCode)
    {
        var nameResult = SetDisplayName(displayName);
        if (nameResult.IsFailure)
        {
            return nameResult;
        }

        if (regionCode is not null)
        {
            RegionCode = regionCode;
        }

        return Result.Success();
    }

    public Result<DateTimeOffset> RequestDeletion(DateTimeOffset now, TimeSpan gracePeriod)
    {
        if (Status == UserStatus.PendingDeletion)
        {
            return Result.Failure<DateTimeOffset>(IdentityErrors.AccountPendingDeletion);
        }

        Status = UserStatus.PendingDeletion;
        DeletionRequestedAt = now;
        var scheduledFor = now + gracePeriod;
        Raise(new UserDeletionRequested(Id, scheduledFor));
        return Result.Success(scheduledFor);
    }

    public Result CancelDeletion()
    {
        if (Status != UserStatus.PendingDeletion)
        {
            return Result.Failure(IdentityErrors.AccountNotPendingDeletion);
        }

        Status = UserStatus.Active;
        DeletionRequestedAt = null;
        return Result.Success();
    }

    public bool IsDueForPurge(DateTimeOffset now, TimeSpan gracePeriod) =>
        Status == UserStatus.PendingDeletion && DeletionRequestedAt + gracePeriod <= now;

    public void MarkPurged() => Raise(new UserPurged(Id));

    private Result SetDisplayName(string? displayName)
    {
        if (displayName is null)
        {
            return Result.Success();
        }

        var trimmed = displayName.Trim();
        if (trimmed.Length > DisplayNameMaxLength)
        {
            return Result.Failure(IdentityErrors.DisplayNameInvalid);
        }

        DisplayName = trimmed.Length == 0 ? null : trimmed;
        return Result.Success();
    }
}
