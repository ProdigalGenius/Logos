using MediatR;
using Microsoft.Extensions.Options;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Options;

namespace SahaNet.Application.Retention;

/// <summary>Toplu silme işlemleri (EF ExecuteDelete ile Infrastructure'da uygulanır).</summary>
public interface IDataRetentionStore
{
    Task<int> DeleteOtpChallengesCreatedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken);

    Task<int> DeleteInactiveRefreshTokensBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken);

    Task<int> DeleteAlertDeliveriesBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken);

    Task<int> DeleteAlertsEndedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken);

    Task<int> DeleteDevicesNotSeenSinceAsync(DateTimeOffset threshold, CancellationToken cancellationToken);
}

public sealed record RetentionResult(int OtpChallenges, int RefreshTokens, int AlertDeliveries, int Alerts, int Devices);

/// <summary>Günlük iş: saklama süresi dolan kayıtları siler.</summary>
public sealed record ApplyRetentionPolicyCommand : IRequest<RetentionResult>;

internal sealed class ApplyRetentionPolicyCommandHandler(
    IDataRetentionStore store,
    IDateTimeProvider clock,
    IOptions<RetentionOptions> options)
    : IRequestHandler<ApplyRetentionPolicyCommand, RetentionResult>
{
    public async Task<RetentionResult> Handle(ApplyRetentionPolicyCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var o = options.Value;

        return new RetentionResult(
            await store.DeleteOtpChallengesCreatedBeforeAsync(now - o.OtpChallenges, cancellationToken),
            await store.DeleteInactiveRefreshTokensBeforeAsync(now - o.InactiveRefreshTokens, cancellationToken),
            await store.DeleteAlertDeliveriesBeforeAsync(now - o.AlertDeliveries, cancellationToken),
            await store.DeleteAlertsEndedBeforeAsync(now - o.ExpiredAlerts, cancellationToken),
            await store.DeleteDevicesNotSeenSinceAsync(now - o.StaleDevices, cancellationToken));
    }
}
