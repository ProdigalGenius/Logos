using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SahaNet.Infrastructure.Options;

namespace SahaNet.Infrastructure.Jobs;

/// <summary>Periyodik işleri açılışta kaydeder (idempotent; cron değişikliği güncellenir).</summary>
internal sealed class RecurringJobRegistrar(IRecurringJobManager manager, IOptions<BackgroundJobOptions> options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var o = options.Value;
        var jobOptions = new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById(o.TimeZone) };

        manager.AddOrUpdate<PlatformJobs>(PlatformJobs.WeatherCheckJobId, j => j.CheckWeatherAsync(CancellationToken.None), o.WeatherCheckCron, jobOptions);
        manager.AddOrUpdate<PlatformJobs>(PlatformJobs.AccountPurgeJobId, j => j.PurgeAccountsAsync(CancellationToken.None), o.AccountPurgeCron, jobOptions);
        manager.AddOrUpdate<PlatformJobs>(PlatformJobs.ListingCloseJobId, j => j.CloseDueListingsAsync(CancellationToken.None), o.ListingCloseCron, jobOptions);
        manager.AddOrUpdate<PlatformJobs>(PlatformJobs.PaymentMaintenanceJobId, j => j.RunPaymentMaintenanceAsync(CancellationToken.None), o.PaymentMaintenanceCron, jobOptions);
        manager.AddOrUpdate<PlatformJobs>(PlatformJobs.RetentionJobId, j => j.ApplyRetentionAsync(CancellationToken.None), o.RetentionCron, jobOptions);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
