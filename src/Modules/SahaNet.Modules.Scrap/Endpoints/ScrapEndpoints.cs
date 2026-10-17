using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SahaNet.Domain.Common;
using SahaNet.Modules.Scrap.Application;

namespace SahaNet.Modules.Scrap.Endpoints;

internal static class ScrapEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/materials", async (ISender sender, CancellationToken ct) => TypedResults.Ok(await sender.Send(new GetMaterialsQuery(), ct)))
            .AllowAnonymous()
            .WithName("GetScrapMaterials");

        group.MapGet("/prices", async (string? material, string? province, int? days, ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new GetScrapPricesQuery(material, province, days ?? 7), ct)))
            .AllowAnonymous()
            .WithName("GetScrapPrices")
            .WithSummary("Günlük hurda fiyatları (TL/kg, bildirimlerin medyanı).");

        group.MapPost("/prices", async (ReportScrapPriceCommand command, ISender sender, CancellationToken ct) => (await sender.Send(command, ct)).ToHttp())
            .RequireAuthorization()
            .WithName("ReportScrapPrice");

        var pickups = group.MapGroup("/pickups").RequireAuthorization();

        pickups.MapPost("/", async (CreatePickupCommand command, ISender sender, CancellationToken ct) => (await sender.Send(command, ct)).ToHttp())
            .WithName("CreatePickup")
            .WithSummary("\"Evimde hurda var\" toplama talebi. Açık adres yalnızca üstlenen hurdacıya görünür.");

        pickups.MapGet("/open", async (string province, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetOpenPickupsQuery(province), ct)).ToHttp())
            .WithName("GetOpenPickups");

        pickups.MapGet("/mine", async (ISender sender, CancellationToken ct) => (await sender.Send(new GetMyPickupsQuery(), ct)).ToHttp())
            .WithName("GetMyPickups");

        foreach (var action in Enum.GetValues<PickupAction>())
        {
            pickups.MapPost($"/{{id:guid}}/{action.ToString().ToLowerInvariant()}", async (Guid id, ISender sender, CancellationToken ct) =>
                    (await sender.Send(new PickupActionCommand(id, action), ct)).ToHttp())
                .WithName($"Pickup{action}");
        }
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
