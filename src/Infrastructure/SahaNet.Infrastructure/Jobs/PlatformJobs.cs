using Hangfire;
using MediatR;
using SahaNet.Application.Alerts;
using SahaNet.Application.Identity.Privacy;
using SahaNet.Application.Retention;

namespace SahaNet.Infrastructure.Jobs;

/// <summary>Hangfire iş giriş noktaları; her iş ilgili MediatR komutunu çalıştırır.</summary>
public sealed class PlatformJobs(ISender sender)
{
    public const string WeatherCheckJobId = "weather-alert-check";
    public const string AccountPurgeJobId = "account-purge";
    public const string RetentionJobId = "data-retention";

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 0)]
    public Task CheckWeatherAsync(CancellationToken cancellationToken) =>
        sender.Send(new CheckWeatherAlertsCommand(), cancellationToken);

    [AutomaticRetry(Attempts = 5)]
    public Task DispatchAlertAsync(Guid alertId, CancellationToken cancellationToken) =>
        sender.Send(new DispatchAlertCommand(alertId), cancellationToken);

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public Task PurgeAccountsAsync(CancellationToken cancellationToken) =>
        sender.Send(new PurgeDeletedAccountsCommand(), cancellationToken);

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public Task ApplyRetentionAsync(CancellationToken cancellationToken) =>
        sender.Send(new ApplyRetentionPolicyCommand(), cancellationToken);
}
