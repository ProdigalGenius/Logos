using SahaNet.Domain.Common;
using SahaNet.Domain.Events;

namespace SahaNet.Domain.Alerts;

public enum AlertSeverity
{
    Info = 0,
    Warning = 1,

    /// <summary>Fırtına vb. hayati durumlar: sessiz modu deler, push ulaşmazsa SMS gönderilir.</summary>
    Critical = 2,
}

public enum AlertKind
{
    Weather = 0,
    Emergency = 1,
}

public sealed record AlertIssued(Guid AlertId, Guid RegionId, AlertSeverity Severity) : DomainEvent;

/// <summary>Bir bölge için yayınlanan uyarı.</summary>
public sealed class Alert : AggregateRoot<Guid>
{
    private Alert(
        Guid id,
        Guid regionId,
        AlertKind kind,
        AlertSeverity severity,
        string title,
        string message,
        DateTimeOffset validFrom,
        DateTimeOffset validTo,
        string dedupKey,
        DateTimeOffset now)
        : base(id)
    {
        RegionId = regionId;
        Kind = kind;
        Severity = severity;
        Title = title;
        Message = message;
        ValidFrom = validFrom;
        ValidTo = validTo;
        DedupKey = dedupKey;
        CreatedAt = now;
    }

    // EF Core
    private Alert()
        : base(Guid.Empty)
    {
        Title = null!;
        Message = null!;
        DedupKey = null!;
    }

    public Guid RegionId { get; private set; }

    public AlertKind Kind { get; private set; }

    public AlertSeverity Severity { get; private set; }

    public string Title { get; private set; }

    public string Message { get; private set; }

    public DateTimeOffset ValidFrom { get; private set; }

    public DateTimeOffset ValidTo { get; private set; }

    /// <summary>Aynı olay için tekrar uyarı üretilmesini engelleyen anahtar.</summary>
    public string DedupKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Alert Issue(
        Guid regionId,
        AlertKind kind,
        AlertSeverity severity,
        string title,
        string message,
        DateTimeOffset validFrom,
        DateTimeOffset validTo,
        string dedupKey,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(dedupKey);
        if (validTo <= validFrom)
        {
            throw new ArgumentException("Bitiş başlangıçtan sonra olmalı.", nameof(validTo));
        }

        var alert = new Alert(Guid.NewGuid(), regionId, kind, severity, title, message, validFrom, validTo, dedupKey, now);
        alert.Raise(new AlertIssued(alert.Id, regionId, severity));
        return alert;
    }

    public bool IsActive(DateTimeOffset now) => now < ValidTo;
}
