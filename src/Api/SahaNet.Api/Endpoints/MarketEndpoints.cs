using MediatR;
using SahaNet.Application.Market;

namespace SahaNet.Api.Endpoints;

internal static class MarketEndpoints
{
    public const string ChatRateLimitPolicy = "chat";

    public static IEndpointRouteBuilder MapMarketEndpoints(this IEndpointRouteBuilder app)
    {
        var market = app.MapGroup("/api/market").WithTags("Market");

        market.MapGet("/categories", async (string? niche, ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new GetCategoriesQuery(niche), ct)))
            .AllowAnonymous()
            .WithName("GetListingCategories");

        market.MapGet("/listings", async (
                    string? niche, string? category, string? region, string? type, string? q, int? page, int? pageSize,
                    ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new SearchListingsQuery(niche, category, region, type, q, page ?? 1, pageSize ?? 20), ct)))
            .AllowAnonymous()
            .WithName("SearchListings");

        market.MapGet("/listings/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetListingQuery(id), ct)).ToHttp())
            .AllowAnonymous()
            .WithName("GetListing");

        market.MapPost("/listings", async (CreateListingCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToHttp())
            .RequireAuthorization()
            .WithName("CreateListing")
            .WithSummary("Sabit fiyat, teklif toplama veya süreli ihale ilanı (kategori alanları modül şemasına göre).");

        market.MapPost("/listings/{id:guid}/cancel", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CancelListingCommand(id), ct)).ToHttp())
            .RequireAuthorization()
            .WithName("CancelListing");

        market.MapPost("/listings/{id:guid}/offers", async (Guid id, OfferRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new PlaceOfferCommand(id, body.UnitPrice, body.Quantity, body.Message), ct)).ToHttp())
            .RequireAuthorization()
            .WithName("PlaceOffer")
            .WithSummary("Teklif, ön sipariş (sabit fiyat + miktar) veya ihale teklifi.");

        market.MapPost("/offers/{id:guid}/accept", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DecideOfferCommand(id, OfferDecision.Accept), ct)).ToHttp())
            .RequireAuthorization()
            .WithName("AcceptOffer");

        market.MapPost("/offers/{id:guid}/reject", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DecideOfferCommand(id, OfferDecision.Reject), ct)).ToHttp())
            .RequireAuthorization()
            .WithName("RejectOffer");

        market.MapPost("/offers/{id:guid}/withdraw", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DecideOfferCommand(id, OfferDecision.Withdraw), ct)).ToHttp())
            .RequireAuthorization()
            .WithName("WithdrawOffer");

        var me = app.MapGroup("/api/me").WithTags("Me").RequireAuthorization();
        me.MapGet("/listings", async (ISender sender, CancellationToken ct) => (await sender.Send(new GetMyListingsQuery(), ct)).ToHttp())
            .WithName("GetMyListings");
        me.MapGet("/offers", async (ISender sender, CancellationToken ct) => (await sender.Send(new GetMyOffersQuery(), ct)).ToHttp())
            .WithName("GetMyOffers");

        var chat = app.MapGroup("/api/conversations").WithTags("Chat").RequireAuthorization();

        chat.MapGet("/", async (ISender sender, CancellationToken ct) => (await sender.Send(new GetMyConversationsQuery(), ct)).ToHttp())
            .WithName("GetMyConversations");

        chat.MapPost("/", async (StartConversationCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToHttp())
            .WithName("StartConversation")
            .WithSummary("İlan sahibiyle (listingId) veya bir kullanıcıyla (userId) sohbet başlat; varsa mevcut sohbet döner.");

        chat.MapGet("/{id:guid}/messages", async (Guid id, DateTimeOffset? before, int? limit, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetMessagesQuery(id, before, limit ?? 50), ct)).ToHttp())
            .WithName("GetMessages");

        chat.MapPost("/{id:guid}/messages", async (Guid id, MessageRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SendMessageCommand(id, body.Text, body.Kind, body.MediaUrl), ct)).ToHttp())
            .RequireRateLimiting(ChatRateLimitPolicy)
            .WithName("SendMessage");

        return app;
    }
}

internal sealed record OfferRequest(decimal UnitPrice, decimal? Quantity, string? Message);

internal sealed record MessageRequest(string? Text, string? Kind, string? MediaUrl);
