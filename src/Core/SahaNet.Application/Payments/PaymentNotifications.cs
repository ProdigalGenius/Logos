using MediatR;
using SahaNet.Application.Events;
using SahaNet.Application.Market;
using SahaNet.Domain.Payments;

namespace SahaNet.Application.Payments;

internal sealed class PaymentNotificationHandlers(IUserNotifier notifier) :
    INotificationHandler<DomainEventNotification<PaymentHeld>>,
    INotificationHandler<DomainEventNotification<PaymentDeliveryReported>>,
    INotificationHandler<DomainEventNotification<PaymentReleased>>,
    INotificationHandler<DomainEventNotification<PaymentRefunded>>,
    INotificationHandler<DomainEventNotification<PaymentDisputed>>
{
    public Task Handle(DomainEventNotification<PaymentHeld> notification, CancellationToken cancellationToken) =>
        Notify(notification.DomainEvent.SellerId, notification.DomainEvent.OrderId, "Ödeme emanette", "Alıcı ödedi. Ürünü teslim edip uygulamada \"Teslim ettim\"e basın.", cancellationToken);

    public Task Handle(DomainEventNotification<PaymentDeliveryReported> notification, CancellationToken cancellationToken) =>
        Notify(notification.DomainEvent.BuyerId, notification.DomainEvent.OrderId, "Teslim bildirildi", "Ürünü aldıysanız onaylayın; sorun varsa itiraz edin.", cancellationToken);

    public Task Handle(DomainEventNotification<PaymentReleased> notification, CancellationToken cancellationToken) =>
        Notify(notification.DomainEvent.SellerId, notification.DomainEvent.OrderId, "Ödemeniz aktarıldı", $"{notification.DomainEvent.Payout:0.00} TL hesabınıza geçiyor.", cancellationToken);

    public Task Handle(DomainEventNotification<PaymentRefunded> notification, CancellationToken cancellationToken) =>
        Notify(notification.DomainEvent.BuyerId, notification.DomainEvent.OrderId, "İade edildi", $"{notification.DomainEvent.Amount:0.00} TL iade ediliyor.", cancellationToken);

    public Task Handle(DomainEventNotification<PaymentDisputed> notification, CancellationToken cancellationToken) =>
        Notify(notification.DomainEvent.SellerId, notification.DomainEvent.OrderId, "İtiraz açıldı", "Alıcı itiraz etti; ödeme yönetici kararına kadar emanette bekleyecek.", cancellationToken);

    private Task Notify(Guid userId, Guid orderId, string title, string body, CancellationToken cancellationToken) =>
        notifier.NotifyAsync(userId, title, body, new Dictionary<string, string> { ["type"] = "payment", ["orderId"] = orderId.ToString() }, cancellationToken);
}
