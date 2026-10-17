using MediatR;
using SahaNet.Application.Events;
using SahaNet.Domain.Market;

namespace SahaNet.Application.Market;

/// <summary>Pazar olaylarını ilgili kullanıcılara push bildirimi olarak iletir.</summary>
internal sealed class MarketNotificationHandlers(IUserNotifier notifier, IListingRepository listings) :
    INotificationHandler<DomainEventNotification<OfferPlaced>>,
    INotificationHandler<DomainEventNotification<OfferAccepted>>,
    INotificationHandler<DomainEventNotification<OfferRejected>>,
    INotificationHandler<DomainEventNotification<BidOutbid>>
{
    public async Task Handle(DomainEventNotification<OfferPlaced> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        await NotifyAsync(e.SellerId, e.ListingId, "Yeni teklif", "ilanınıza yeni teklif geldi.", cancellationToken);
    }

    public async Task Handle(DomainEventNotification<OfferAccepted> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        await NotifyAsync(e.BuyerId, e.ListingId, "Teklifiniz kabul edildi", "satıcı teklifinizi kabul etti.", cancellationToken);
    }

    public async Task Handle(DomainEventNotification<OfferRejected> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        await NotifyAsync(e.BuyerId, e.ListingId, "Teklif sonuçlandı", "teklifiniz kabul edilmedi.", cancellationToken);
    }

    public async Task Handle(DomainEventNotification<BidOutbid> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        await NotifyAsync(e.BuyerId, e.ListingId, "Teklifiniz geçildi", $"yeni en yüksek teklif {e.NewHighest:0.##} TL.", cancellationToken);
    }

    private async Task NotifyAsync(Guid userId, Guid listingId, string title, string suffix, CancellationToken cancellationToken)
    {
        var listing = await listings.GetByIdAsync(listingId, cancellationToken);
        var name = listing?.Title ?? "İlan";
        await notifier.NotifyAsync(
            userId,
            title,
            $"\"{name}\": {suffix}",
            new Dictionary<string, string> { ["type"] = "listing", ["listingId"] = listingId.ToString() },
            cancellationToken);
    }
}
