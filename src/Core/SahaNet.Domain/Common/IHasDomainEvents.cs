using SahaNet.Domain.Events;

namespace SahaNet.Domain.Common;

/// <summary>Kalıcılaştırma sırasında event toplamak için jenerik olmayan arayüz.</summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
