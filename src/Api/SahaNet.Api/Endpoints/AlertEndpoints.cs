using MediatR;
using SahaNet.Application.Alerts;

namespace SahaNet.Api.Endpoints;

internal static class AlertEndpoints
{
    public static IEndpointRouteBuilder MapAlertEndpoints(this IEndpointRouteBuilder app)
    {
        var geo = app.MapGroup("/api/geo").WithTags("Geo").AllowAnonymous();

        geo.MapGet("/regions", async (ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new GetRegionsQuery(), ct)))
            .WithName("GetGeoRegions");

        geo.MapGet("/regions/nearby", async (double lat, double lon, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetNearbyRegionsQuery(lat, lon), ct)).ToHttp())
            .WithName("GetNearbyRegions")
            .WithSummary("Konumu kapsayan bölgeler. Konum saklanmaz.");

        geo.MapGet("/regions/{regionId:guid}/forecast", async (Guid regionId, int? days, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetRegionForecastQuery(regionId, days ?? 3), ct)).ToHttp())
            .WithName("GetRegionForecast")
            .WithSummary("Saatlik rüzgâr ve dalga tahmini (en fazla 3 gün) ve aktif uyarılar.");

        app.MapGet("/api/alerts/active", async (Guid? regionId, ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new GetActiveAlertsQuery(regionId), ct)))
            .WithTags("Alerts")
            .AllowAnonymous()
            .WithName("GetActiveAlerts");

        var me = app.MapGroup("/api/me").WithTags("Me").RequireAuthorization();

        me.MapGet("/subscriptions", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetMySubscriptionsQuery(), ct)).ToHttp())
            .WithName("GetMySubscriptions");

        me.MapPut("/subscriptions/{regionId:guid}", async (Guid regionId, SubscriptionTopicsRequest? body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SubscribeToRegionCommand(regionId, body?.Topics), ct)).ToHttp())
            .WithName("SubscribeToRegion")
            .WithSummary("Bölgeye abone ol veya konuları güncelle. Kritik uyarılar konudan bağımsız gelir.");

        me.MapDelete("/subscriptions/{regionId:guid}", async (Guid regionId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UnsubscribeFromRegionCommand(regionId), ct)).ToHttp())
            .WithName("UnsubscribeFromRegion");

        me.MapPost("/devices", async (RegisterDeviceCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToHttp())
            .WithName("RegisterDevice");

        me.MapPost("/devices/unregister", async (UnregisterDeviceCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToHttp())
            .WithName("UnregisterDevice");

        return app;
    }
}

internal sealed record SubscriptionTopicsRequest(IReadOnlyList<string>? Topics);
