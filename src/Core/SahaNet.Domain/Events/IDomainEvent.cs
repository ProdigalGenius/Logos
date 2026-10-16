namespace SahaNet.Domain.Events;

/// <summary>
/// Modüller arası iletişimin tek yolu. Modüller birbirini referans almaz,
/// yalnızca domain event'leri üzerinden haberleşir.
/// </summary>
/// <remarks>
/// Domain katmanı bağımlılıksız kalsın diye MediatR'a bağlanmaz; Infrastructure
/// event'i Application katmanındaki DomainEventNotification sarmalayıcısıyla yayınlar.
/// </remarks>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }
}

/// <summary>Domain event'leri için temel kayıt tipi.</summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
