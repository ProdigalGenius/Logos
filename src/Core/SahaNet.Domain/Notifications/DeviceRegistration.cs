using SahaNet.Domain.Common;

namespace SahaNet.Domain.Notifications;

public enum DevicePlatform
{
    Android = 0,
    Ios = 1,
}

/// <summary>Push bildirimi için cihaz kaydı (FCM token).</summary>
public sealed class DeviceRegistration : Entity<Guid>
{
    public const int TokenMaxLength = 4096;

    private DeviceRegistration(Guid id, Guid userId, string token, string tokenHash, DevicePlatform platform, DateTimeOffset now)
        : base(id)
    {
        UserId = userId;
        Token = token;
        TokenHash = tokenHash;
        Platform = platform;
        CreatedAt = now;
        LastSeenAt = now;
    }

    // EF Core
    private DeviceRegistration()
        : base(Guid.Empty)
    {
        Token = null!;
        TokenHash = null!;
    }

    public Guid UserId { get; private set; }

    public string Token { get; private set; }

    /// <summary>Token benzersizliği için özet (token'lar uzun olduğundan indekslenmez).</summary>
    public string TokenHash { get; private set; }

    public DevicePlatform Platform { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public static DeviceRegistration Register(Guid userId, string token, string tokenHash, DevicePlatform platform, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        return new DeviceRegistration(Guid.NewGuid(), userId, token, tokenHash, platform, now);
    }

    /// <summary>Aynı token başka bir hesapta girildiyse (cihaz el değiştirdi) yeni sahibine geçer.</summary>
    public void Touch(Guid userId, DevicePlatform platform, DateTimeOffset now)
    {
        UserId = userId;
        Platform = platform;
        LastSeenAt = now;
    }
}
