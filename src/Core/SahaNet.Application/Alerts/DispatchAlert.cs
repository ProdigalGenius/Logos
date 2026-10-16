using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Events;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Application.Options;
using SahaNet.Domain.Alerts;
using SahaNet.Domain.Geo;
using SahaNet.Domain.Identity;

namespace SahaNet.Application.Alerts;

/// <summary>Uyarı yayınlanınca dağıtımı arka plana planlar (kaydı bekletmez).</summary>
internal sealed class ScheduleDispatchOnAlertIssued(IAlertDispatchScheduler scheduler)
    : INotificationHandler<DomainEventNotification<AlertIssued>>
{
    public Task Handle(DomainEventNotification<AlertIssued> notification, CancellationToken cancellationToken) =>
        scheduler.ScheduleAsync(notification.DomainEvent.AlertId, cancellationToken);
}

public sealed record DispatchAlertResult(int Recipients, int PushSent, int SmsSent, int Skipped);

/// <summary>
/// Uyarıyı bölge abonelerine ulaştırır. Kurallar:
/// <list type="bullet">
/// <item>Uyarı seviyesi yalnızca Weather konusuna abone olanlara gider; kritik uyarı tüm abonelere.</item>
/// <item>Önce push; kritik uyarı hiçbir cihaza ulaşmazsa SMS yedeği.</item>
/// <item>İş tekrar çalışırsa (Hangfire retry) daha önce ulaşılan kullanıcı atlanır.</item>
/// </list>
/// </summary>
public sealed record DispatchAlertCommand(Guid AlertId) : IRequest<DispatchAlertResult>;

internal sealed partial class DispatchAlertCommandHandler(
    IAlertRepository alerts,
    IRegionRepository regions,
    ISubscriptionRepository subscriptions,
    IDeviceRepository devices,
    IUserRepository users,
    IPushSender push,
    ISmsSender sms,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    IOptions<AlertOptions> options,
    ILogger<DispatchAlertCommandHandler> logger)
    : IRequestHandler<DispatchAlertCommand, DispatchAlertResult>
{
    public async Task<DispatchAlertResult> Handle(DispatchAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = await alerts.GetByIdAsync(request.AlertId, cancellationToken);
        if (alert is null || !alert.IsActive(clock.UtcNow))
        {
            return new DispatchAlertResult(0, 0, 0, 0);
        }

        var region = await regions.GetByIdAsync(alert.RegionId, cancellationToken);
        var delivered = await alerts.GetDeliveredUserIdsAsync(alert.Id, cancellationToken);
        var subscribers = await subscriptions.GetByRegionAsync(alert.RegionId, cancellationToken);
        var isCritical = alert.Severity == AlertSeverity.Critical;
        var message = new PushMessage(
            alert.Title,
            alert.Message,
            alert.Severity,
            new Dictionary<string, string>
            {
                ["type"] = "alert",
                ["alertId"] = alert.Id.ToString(),
                ["regionCode"] = region?.Code ?? string.Empty,
                ["severity"] = alert.Severity.ToString(),
            });

        int recipients = 0, pushSent = 0, smsSent = 0, skipped = 0;

        foreach (var subscription in subscribers)
        {
            if (delivered.Contains(subscription.UserId) || (!isCritical && !subscription.Wants(AlertTopics.Weather)))
            {
                skipped++;
                continue;
            }

            var user = await users.GetByIdAsync(subscription.UserId, cancellationToken);
            if (user is null || user.Status != UserStatus.Active)
            {
                skipped++;
                continue;
            }

            recipients++;
            var now = clock.UtcNow;
            var reachedByPush = false;

            foreach (var device in await devices.GetByUserAsync(user.Id, cancellationToken))
            {
                var result = await push.SendAsync(device, message, cancellationToken);
                if (result == PushResult.Sent)
                {
                    reachedByPush = true;
                }
                else if (result == PushResult.InvalidToken)
                {
                    devices.Remove(device);
                }
            }

            if (reachedByPush)
            {
                pushSent++;
                alerts.AddDelivery(AlertDelivery.Record(alert.Id, user.Id, DeliveryChannel.Push, DeliveryStatus.Sent, now));
            }
            else if (isCritical && options.Value.SmsFallbackForCritical)
            {
                var smsStatus = await TrySendSmsAsync(user, alert, cancellationToken);
                smsSent += smsStatus == DeliveryStatus.Sent ? 1 : 0;
                alerts.AddDelivery(AlertDelivery.Record(alert.Id, user.Id, DeliveryChannel.Sms, smsStatus, now));
            }
            else
            {
                alerts.AddDelivery(AlertDelivery.Record(alert.Id, user.Id, DeliveryChannel.Push, DeliveryStatus.Failed, now));
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new DispatchAlertResult(recipients, pushSent, smsSent, skipped);
    }

    private async Task<DeliveryStatus> TrySendSmsAsync(User user, Alert alert, CancellationToken cancellationToken)
    {
        try
        {
            await sms.SendAsync(user.Phone, $"{alert.Title}: {alert.Message}", cancellationToken);
            return DeliveryStatus.Sent;
        }
        catch (HttpRequestException ex)
        {
            LogSmsFailed(logger, ex, alert.Id);
            return DeliveryStatus.Failed;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Kritik uyarı SMS'i gönderilemedi: {AlertId}")]
    private static partial void LogSmsFailed(ILogger logger, Exception exception, Guid alertId);
}
