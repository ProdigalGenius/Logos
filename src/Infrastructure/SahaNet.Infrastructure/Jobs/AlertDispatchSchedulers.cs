using Hangfire;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SahaNet.Application.Alerts;

namespace SahaNet.Infrastructure.Jobs;

/// <summary>Dağıtımı Hangfire kuyruğuna atar (yeniden deneme ve kalıcılık için).</summary>
internal sealed class HangfireAlertDispatchScheduler(IBackgroundJobClient jobs) : IAlertDispatchScheduler
{
    public Task ScheduleAsync(Guid alertId, CancellationToken cancellationToken)
    {
        jobs.Enqueue<PlatformJobs>(j => j.DispatchAlertAsync(alertId, CancellationToken.None));
        return Task.CompletedTask;
    }
}

/// <summary>Hangfire kapalıyken (test/geliştirme) dağıtımı ayrı bir kapsamda hemen çalıştırır.</summary>
internal sealed class InlineAlertDispatchScheduler(IServiceScopeFactory scopeFactory) : IAlertDispatchScheduler
{
    public async Task ScheduleAsync(Guid alertId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new DispatchAlertCommand(alertId), cancellationToken);
    }
}
