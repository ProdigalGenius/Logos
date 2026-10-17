using MediatR;
using SahaNet.Domain.Events;

namespace SahaNet.Application.Events;

/// <summary>
/// Domain event'ini MediatR bildirimine saran tip. Modüller başka modüllerin
/// event'lerini <c>INotificationHandler&lt;DomainEventNotification&lt;TEvent&gt;&gt;</c> ile dinler.
/// </summary>
public sealed record DomainEventNotification<TEvent>(TEvent DomainEvent) : INotification
    where TEvent : IDomainEvent;

public static class DomainEventNotification
{
    /// <summary>Çalışma zamanı tipine göre doğru jenerik sarmalayıcıyı oluşturur.</summary>
    public static INotification Create(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var type = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
        return (INotification)Activator.CreateInstance(type, domainEvent)!;
    }
}
