using FluentValidation;
using MediatR;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Weather;
using SahaNet.Domain.Alerts;
using SahaNet.Domain.Common;
using SahaNet.Domain.Geo;

namespace SahaNet.Application.Alerts;

public sealed record RegionResponse(Guid Id, string Code, string Name, string Kind, double Latitude, double Longitude, double RadiusKm, string? ProvinceCode);

public sealed record NearbyRegionResponse(RegionResponse Region, double DistanceKm);

public sealed record AlertResponse(Guid Id, Guid RegionId, string Kind, string Severity, string Title, string Message, DateTimeOffset ValidFrom, DateTimeOffset ValidTo);

public sealed record ForecastResponse(RegionResponse Region, DateTimeOffset RetrievedAt, IReadOnlyList<WeatherHour> Hours, IReadOnlyList<AlertResponse> ActiveAlerts);

internal static class AlertMapping
{
    public static RegionResponse ToResponse(this Region r) =>
        new(r.Id, r.Code, r.Name, r.Kind.ToString(), r.Center.Latitude, r.Center.Longitude, r.RadiusKm, r.ProvinceCode);

    public static AlertResponse ToResponse(this Alert a) =>
        new(a.Id, a.RegionId, a.Kind.ToString(), a.Severity.ToString(), a.Title, a.Message, a.ValidFrom, a.ValidTo);
}

// ---- Bölgeler ----

public sealed record GetRegionsQuery : IRequest<IReadOnlyList<RegionResponse>>;

internal sealed class GetRegionsQueryHandler(IRegionRepository regions) : IRequestHandler<GetRegionsQuery, IReadOnlyList<RegionResponse>>
{
    public async Task<IReadOnlyList<RegionResponse>> Handle(GetRegionsQuery request, CancellationToken cancellationToken) =>
        (await regions.GetActiveAsync(cancellationToken)).Select(r => r.ToResponse()).ToList();
}

/// <summary>Konumu kapsayan bölgeler. KVKK: konum yalnızca sorgu için kullanılır, saklanmaz.</summary>
public sealed record GetNearbyRegionsQuery(double Latitude, double Longitude) : IRequest<Result<IReadOnlyList<NearbyRegionResponse>>>;

internal sealed class GetNearbyRegionsQueryHandler(IRegionRepository regions)
    : IRequestHandler<GetNearbyRegionsQuery, Result<IReadOnlyList<NearbyRegionResponse>>>
{
    public async Task<Result<IReadOnlyList<NearbyRegionResponse>>> Handle(GetNearbyRegionsQuery request, CancellationToken cancellationToken)
    {
        var point = GeoPoint.Create(request.Latitude, request.Longitude);
        if (point.IsFailure)
        {
            return Result.Failure<IReadOnlyList<NearbyRegionResponse>>(point.Error);
        }

        var found = await regions.GetContainingAsync(point.Value, cancellationToken);
        IReadOnlyList<NearbyRegionResponse> response = found
            .Select(r => new NearbyRegionResponse(r.ToResponse(), Math.Round(r.Center.DistanceKmTo(point.Value), 1)))
            .OrderBy(r => r.DistanceKm)
            .ToList();
        return Result.Success(response);
    }
}

// ---- Bölge tahmini (deniz durumu) ----

public sealed record GetRegionForecastQuery(Guid RegionId, int Days = 3) : IRequest<Result<ForecastResponse>>;

internal sealed class GetRegionForecastQueryValidator : AbstractValidator<GetRegionForecastQuery>
{
    public GetRegionForecastQueryValidator() => RuleFor(x => x.Days).InclusiveBetween(1, 3);
}

internal sealed class GetRegionForecastQueryHandler(
    IRegionRepository regions,
    IAlertRepository alerts,
    IWeatherProvider weather,
    IDateTimeProvider clock)
    : IRequestHandler<GetRegionForecastQuery, Result<ForecastResponse>>
{
    public async Task<Result<ForecastResponse>> Handle(GetRegionForecastQuery request, CancellationToken cancellationToken)
    {
        var region = await regions.GetByIdAsync(request.RegionId, cancellationToken);
        if (region is null)
        {
            return Result.Failure<ForecastResponse>(AlertErrors.RegionNotFound);
        }

        var forecast = await weather.GetForecastAsync(region.Center, request.Days, cancellationToken);
        var active = await alerts.GetActiveAsync([region.Id], clock.UtcNow, cancellationToken);

        return Result.Success(new ForecastResponse(
            region.ToResponse(),
            forecast.RetrievedAt,
            forecast.Hours,
            active.Select(a => a.ToResponse()).ToList()));
    }
}

// ---- Aktif uyarılar ----

public sealed record GetActiveAlertsQuery(Guid? RegionId) : IRequest<IReadOnlyList<AlertResponse>>;

internal sealed class GetActiveAlertsQueryHandler(IAlertRepository alerts, IDateTimeProvider clock)
    : IRequestHandler<GetActiveAlertsQuery, IReadOnlyList<AlertResponse>>
{
    public async Task<IReadOnlyList<AlertResponse>> Handle(GetActiveAlertsQuery request, CancellationToken cancellationToken)
    {
        var active = await alerts.GetActiveAsync(request.RegionId is { } id ? [id] : null, clock.UtcNow, cancellationToken);
        return active.OrderByDescending(a => a.Severity).ThenBy(a => a.ValidFrom).Select(a => a.ToResponse()).ToList();
    }
}
