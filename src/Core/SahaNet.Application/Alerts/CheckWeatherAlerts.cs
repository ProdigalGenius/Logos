using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Options;
using SahaNet.Application.Weather;
using SahaNet.Domain.Alerts;

namespace SahaNet.Application.Alerts;

public sealed record CheckWeatherAlertsResult(int RegionsChecked, int AlertsIssued, int Failures);

/// <summary>Periyodik iş: abonesi olan her bölge için tahmini alır, eşik aşımı varsa uyarı yayınlar.</summary>
public sealed record CheckWeatherAlertsCommand : IRequest<CheckWeatherAlertsResult>;

internal sealed partial class CheckWeatherAlertsCommandHandler(
    IRegionRepository regions,
    IAlertRepository alerts,
    IWeatherProvider weather,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    IOptions<AlertOptions> options,
    ILogger<CheckWeatherAlertsCommandHandler> logger)
    : IRequestHandler<CheckWeatherAlertsCommand, CheckWeatherAlertsResult>
{
    public async Task<CheckWeatherAlertsResult> Handle(CheckWeatherAlertsCommand request, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.DisplayTimeZone);
        var now = clock.UtcNow;
        var targets = await regions.GetWithSubscribersAsync(cancellationToken);
        int issued = 0, failures = 0;

        foreach (var region in targets)
        {
            try
            {
                var forecast = await weather.GetForecastAsync(region.Center, 2, cancellationToken);
                var candidate = WeatherAlertEvaluator.Evaluate(forecast, now, settings);
                if (candidate is null)
                {
                    continue;
                }

                var dedupKey = WeatherAlertEvaluator.DedupKey(region.Id, candidate);
                if (await alerts.ExistsAsync(dedupKey, cancellationToken))
                {
                    continue;
                }

                var (title, message) = WeatherAlertEvaluator.Compose(region.Name, candidate, zone);
                alerts.Add(Alert.Issue(region.Id, AlertKind.Weather, candidate.Severity, title, message, candidate.From, candidate.To, dedupKey, now));

                // Her uyarı ayrı kaydedilir; AlertIssued event'i dağıtımı planlar.
                await unitOfWork.SaveChangesAsync(cancellationToken);
                issued++;
            }
            catch (Exception ex) when (ex is HttpRequestException or TimeoutException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                // Tek bölgenin hatası diğer bölgelerin kontrolünü engellememeli.
                failures++;
                LogRegionFailed(logger, ex, region.Code);
            }
        }

        return new CheckWeatherAlertsResult(targets.Count, issued, failures);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Hava kontrolü başarısız: {RegionCode}")]
    private static partial void LogRegionFailed(ILogger logger, Exception exception, string regionCode);
}
