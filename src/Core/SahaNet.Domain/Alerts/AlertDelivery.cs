using SahaNet.Domain.Common;

namespace SahaNet.Domain.Alerts;

public enum DeliveryChannel
{
    Push = 0,
    Sms = 1,
}

public enum DeliveryStatus
{
    Sent = 0,
    Failed = 1,
}

/// <summary>Uyarının bir kullanıcıya hangi kanaldan ulaştırıldığının kaydı (tekrar göndermeyi önler).</summary>
public sealed class AlertDelivery : Entity<Guid>
{
    private AlertDelivery(Guid id, Guid alertId, Guid userId, DeliveryChannel channel, DeliveryStatus status, DateTimeOffset now)
        : base(id)
    {
        AlertId = alertId;
        UserId = userId;
        Channel = channel;
        Status = status;
        CreatedAt = now;
    }

    // EF Core
    private AlertDelivery()
        : base(Guid.Empty)
    {
    }

    public Guid AlertId { get; private set; }

    public Guid UserId { get; private set; }

    public DeliveryChannel Channel { get; private set; }

    public DeliveryStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static AlertDelivery Record(Guid alertId, Guid userId, DeliveryChannel channel, DeliveryStatus status, DateTimeOffset now) =>
        new(Guid.NewGuid(), alertId, userId, channel, status, now);
}
