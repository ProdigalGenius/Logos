using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SahaNet.Domain.Common;
using SahaNet.Modules.Fishing.Application;

namespace SahaNet.Modules.Fishing.Endpoints;

internal static class FishingEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/info", () => TypedResults.Ok(new ModuleInfoResponse(FishingModuleDescriptor.ModuleKey, "BalıkçıNet")))
            .WithName("GetFishingModuleInfo")
            .AllowAnonymous();

        // ---- Katalog (giriş gerektirmez) ----
        group.MapGet("/ports", async (double? lat, double? lon, ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new GetPortsQuery(lat, lon), ct)))
            .WithName("GetPorts")
            .AllowAnonymous();

        group.MapGet("/ports/{code}/sea-status", async (string code, int? days, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetSeaStatusQuery(code, days ?? 3), ct)).ToHttp())
            .WithName("GetSeaStatus")
            .WithSummary("Liman bazlı deniz durumu: günlük özet, saatlik tahmin, çıkış kararı ve aktif uyarılar.")
            .AllowAnonymous();

        group.MapGet("/species", async (ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new GetSpeciesQuery(), ct)))
            .WithName("GetSpecies")
            .AllowAnonymous();

        group.MapGet("/bans", async (string? species, string? region, bool? activeOnly, ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new GetBansQuery(species, region, activeOnly ?? false), ct)))
            .WithName("GetFishingBans")
            .WithSummary("Av yasakları takvimi (taslak veri, resmi tebliğle doğrulanmalı).")
            .AllowAnonymous();

        // ---- Av noktası defteri ----
        var spots = group.MapGroup("/spots").RequireAuthorization();

        spots.MapGet("/", async (ISender sender, CancellationToken ct) => (await sender.Send(new GetMySpotsQuery(), ct)).ToHttp())
            .WithName("GetMySpots");

        spots.MapPost("/", async (SaveSpotCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command with { Id = null }, ct)).ToHttp())
            .WithName("CreateSpot");

        spots.MapPut("/{id:guid}", async (Guid id, SaveSpotCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command with { Id = id }, ct)).ToHttp())
            .WithName("UpdateSpot");

        spots.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DeleteSpotCommand(id), ct)).ToHttp())
            .WithName("DeleteSpot");

        spots.MapPost("/{id:guid}/shares", async (Guid id, ShareRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ShareSpotCommand(id, body.Phone), ct)).ToHttp())
            .WithName("ShareSpot")
            .WithSummary("Noktayı telefon numarasıyla bir kullanıcıyla paylaş (konum paylaşımı rızası gerekir).");

        spots.MapDelete("/{id:guid}/shares/{userId:guid}", async (Guid id, Guid userId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UnshareSpotCommand(id, userId), ct)).ToHttp())
            .WithName("UnshareSpot");

        // ---- Av günlüğü ----
        var catches = group.MapGroup("/catches").RequireAuthorization();

        catches.MapGet("/", async (DateTimeOffset? from, DateTimeOffset? to, int? limit, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetMyCatchesQuery(from, to, limit ?? 100), ct)).ToHttp())
            .WithName("GetMyCatches");

        catches.MapPost("/", async (RecordCatchCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToHttp())
            .WithName("RecordCatch")
            .WithSummary("Av kaydı; yasal boy ve yasak dönemi uyarıları döner (kayıt engellenmez).");

        catches.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DeleteCatchCommand(id), ct)).ToHttp())
            .WithName("DeleteCatch");

        catches.MapGet("/stats", async (int? season, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetSeasonStatsQuery(season), ct)).ToHttp())
            .WithName("GetSeasonStats")
            .WithSummary("Sezon istatistiği (1 Eylül – 31 Ağustos).");
    }

    private static IResult ToHttp<T>(this Result<T> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();

    private static IResult ToHttp(this Result result) =>
        result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();

    private static IResult ToProblem(this Error error) => TypedResults.Problem(
        statusCode: error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status500InternalServerError,
        },
        title: error.Code,
        detail: error.Message);
}

internal sealed record ShareRequest(string Phone);
