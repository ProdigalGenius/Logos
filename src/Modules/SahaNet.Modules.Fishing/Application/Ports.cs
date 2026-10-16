using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Alerts;
using SahaNet.Application.Options;
using SahaNet.Application.Weather;
using SahaNet.Domain.Alerts;
using SahaNet.Domain.Common;
using SahaNet.Domain.Geo;
using SahaNet.Modules.Fishing.Domain;
using SahaNet.Modules.Fishing.Persistence;

namespace SahaNet.Modules.Fishing.Application;

public sealed record PortResponse(Guid Id, string Code, string Name, string ProvinceCode, double Latitude, double Longitude, double? DistanceKm);

public sealed record SeaStatusResponse(
    PortResponse Port,
    IReadOnlyList<string> RegionCodes,
    DateTimeOffset RetrievedAt,
    SeaVerdict Now,
    IReadOnlyList<SeaDay> Days,
    IReadOnlyList<SeaHour> Hours,
    IReadOnlyList<AlertResponse> ActiveAlerts);

// ---- Limanlar (isteğe bağlı konuma göre sıralı) ----

public sealed record GetPortsQuery(double? Latitude, double? Longitude) : IRequest<IReadOnlyList<PortResponse>>;

internal sealed class GetPortsQueryHandler(FishingDbContext db) : IRequestHandler<GetPortsQuery, IReadOnlyList<PortResponse>>
{
    public async Task<IReadOnlyList<PortResponse>> Handle(GetPortsQuery request, CancellationToken cancellationToken)
    {
        var ports = await db.Ports.AsNoTracking().ToListAsync(cancellationToken);
        var origin = request is { Latitude: { } lat, Longitude: { } lon } && GeoPoint.Create(lat, lon) is { IsSuccess: true } p ? p.Value : null;

        return ports
            .Select(port => port.ToResponse(origin is null ? null : Math.Round(origin.DistanceKmTo(port.Location), 1)))
            .OrderBy(r => r.DistanceKm ?? 0)
            .ThenBy(r => r.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), false))
            .ToList();
    }
}

internal static class PortMapping
{
    public static PortResponse ToResponse(this Port p, double? distanceKm = null) =>
        new(p.Id, p.Code, p.Name, p.ProvinceCode, p.Location.Latitude, p.Location.Longitude, distanceKm);
}

// ---- Liman bazlı deniz durumu ----

public sealed record GetSeaStatusQuery(string PortCode, int Days = 3) : IRequest<Result<SeaStatusResponse>>;

internal sealed class GetSeaStatusQueryValidator : AbstractValidator<GetSeaStatusQuery>
{
    public GetSeaStatusQueryValidator()
    {
        RuleFor(x => x.PortCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Days).InclusiveBetween(1, 3);
    }
}

internal sealed class GetSeaStatusQueryHandler(
    FishingDbContext db,
    IWeatherProvider weather,
    IRegionRepository regions,
    IAlertRepository alerts,
    IDateTimeProvider clock,
    IOptions<AlertOptions> options)
    : IRequestHandler<GetSeaStatusQuery, Result<SeaStatusResponse>>
{
    public async Task<Result<SeaStatusResponse>> Handle(GetSeaStatusQuery request, CancellationToken cancellationToken)
    {
        var port = await db.Ports.AsNoTracking().FirstOrDefaultAsync(p => p.Code == request.PortCode, cancellationToken);
        if (port is null)
        {
            return Result.Failure<SeaStatusResponse>(FishingErrors.PortNotFound);
        }

        var settings = options.Value;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.DisplayTimeZone);
        var now = clock.UtcNow;
        var forecast = await weather.GetForecastAsync(port.Location, request.Days, cancellationToken);
        var containing = await regions.GetContainingAsync(port.Location, cancellationToken);
        IReadOnlyList<Alert> active = containing.Count == 0
            ? []
            : await alerts.GetActiveAsync(containing.Select(r => r.Id).ToList(), now, cancellationToken);

        var upcoming = forecast.Hours.Where(h => h.Time >= now.AddHours(-1)).ToList();
        var current = upcoming.FirstOrDefault();
        var verdictNow = current is null ? SeaVerdict.Go : SeaStatusCalculator.Verdict(current, settings);
        if (active.Any(a => a.Severity == AlertSeverity.Critical && a.ValidFrom <= now.AddHours(3)))
        {
            verdictNow = SeaVerdict.NoGo;
        }

        return Result.Success(new SeaStatusResponse(
            port.ToResponse(),
            containing.Select(r => r.Code).ToList(),
            forecast.RetrievedAt,
            verdictNow,
            SeaStatusCalculator.Days(upcoming, settings, zone),
            SeaStatusCalculator.Hours(upcoming, settings),
            active.OrderByDescending(a => a.Severity)
                .Select(a => new AlertResponse(a.Id, a.RegionId, a.Kind.ToString(), a.Severity.ToString(), a.Title, a.Message, a.ValidFrom, a.ValidTo))
                .ToList()));
    }
}
