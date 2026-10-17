using System.Globalization;
using System.Text.Json;

namespace SahaNet.Mobile.Core.Api;

/// <summary>Çevrimiçi işlemler: pazar, sohbet, hurda modülü ve KVKK hesap işlemleri.</summary>
public interface ICommunityApi
{
    Task<ApiResult<IReadOnlyList<CategoryDto>>> GetCategoriesAsync(string nicheKey, CancellationToken cancellationToken);

    Task<ApiResult<ListingPageDto>> SearchListingsAsync(string nicheKey, string? text, int page, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<ListingDto>>> GetMyListingsAsync(CancellationToken cancellationToken);

    Task<ApiResult<ListingDto>> CreateListingAsync(CreateListingRequest request, CancellationToken cancellationToken);

    Task<ApiResult<ListingDto>> PlaceOfferAsync(Guid listingId, decimal unitPrice, decimal? quantity, string? message, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<ConversationDto>>> GetConversationsAsync(CancellationToken cancellationToken);

    Task<ApiResult<ConversationDto>> StartConversationAsync(Guid listingId, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<MessageDto>>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken);

    Task<ApiResult<MessageDto>> SendMessageAsync(Guid conversationId, string text, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<MaterialDto>>> GetMaterialsAsync(CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<ScrapPriceDto>>> GetScrapPricesAsync(string? provinceCode, CancellationToken cancellationToken);

    Task<ApiResult<PickupDto>> CreatePickupAsync(CreatePickupRequest request, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<PickupDto>>> GetMyPickupsAsync(CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<PickupDto>>> GetOpenPickupsAsync(string provinceCode, CancellationToken cancellationToken);

    /// <summary>action: claim, release, complete, cancel.</summary>
    Task<ApiResult<PickupDto>> PickupActionAsync(Guid id, string action, CancellationToken cancellationToken);

    Task<ApiResult<string>> ExportMyDataAsync(CancellationToken cancellationToken);

    Task<ApiResult<bool>> RequestAccountDeletionAsync(CancellationToken cancellationToken);
}

public sealed partial class SahaNetApiClient : ICommunityApi
{
    public Task<ApiResult<IReadOnlyList<CategoryDto>>> GetCategoriesAsync(string nicheKey, CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<CategoryDto>>(HttpMethod.Get, $"api/market/categories?niche={Uri.EscapeDataString(nicheKey)}", null, cancellationToken);

    public Task<ApiResult<ListingPageDto>> SearchListingsAsync(string nicheKey, string? text, int page, CancellationToken cancellationToken)
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"api/market/listings?niche={Uri.EscapeDataString(nicheKey)}&page={page}&pageSize=20");
        if (!string.IsNullOrWhiteSpace(text))
        {
            path += "&q=" + Uri.EscapeDataString(text.Trim());
        }

        return SendAsync<ListingPageDto>(HttpMethod.Get, path, null, cancellationToken);
    }

    public Task<ApiResult<IReadOnlyList<ListingDto>>> GetMyListingsAsync(CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<ListingDto>>(HttpMethod.Get, "api/me/listings", null, cancellationToken);

    public Task<ApiResult<ListingDto>> CreateListingAsync(CreateListingRequest request, CancellationToken cancellationToken) =>
        SendAsync<ListingDto>(HttpMethod.Post, "api/market/listings", request, cancellationToken);

    public Task<ApiResult<ListingDto>> PlaceOfferAsync(Guid listingId, decimal unitPrice, decimal? quantity, string? message, CancellationToken cancellationToken) =>
        SendAsync<ListingDto>(HttpMethod.Post, $"api/market/listings/{listingId}/offers", new { unitPrice, quantity, message }, cancellationToken);

    public Task<ApiResult<IReadOnlyList<ConversationDto>>> GetConversationsAsync(CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<ConversationDto>>(HttpMethod.Get, "api/conversations", null, cancellationToken);

    public Task<ApiResult<ConversationDto>> StartConversationAsync(Guid listingId, CancellationToken cancellationToken) =>
        SendAsync<ConversationDto>(HttpMethod.Post, "api/conversations", new { listingId }, cancellationToken);

    public Task<ApiResult<IReadOnlyList<MessageDto>>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<MessageDto>>(HttpMethod.Get, $"api/conversations/{conversationId}/messages?limit=50", null, cancellationToken);

    public Task<ApiResult<MessageDto>> SendMessageAsync(Guid conversationId, string text, CancellationToken cancellationToken) =>
        SendAsync<MessageDto>(HttpMethod.Post, $"api/conversations/{conversationId}/messages", new { text, kind = "Text" }, cancellationToken);

    public Task<ApiResult<IReadOnlyList<MaterialDto>>> GetMaterialsAsync(CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<MaterialDto>>(HttpMethod.Get, "api/scrap/materials", null, cancellationToken);

    public Task<ApiResult<IReadOnlyList<ScrapPriceDto>>> GetScrapPricesAsync(string? provinceCode, CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<ScrapPriceDto>>(
            HttpMethod.Get,
            string.IsNullOrWhiteSpace(provinceCode) ? "api/scrap/prices" : $"api/scrap/prices?province={Uri.EscapeDataString(provinceCode)}",
            null,
            cancellationToken);

    public Task<ApiResult<PickupDto>> CreatePickupAsync(CreatePickupRequest request, CancellationToken cancellationToken) =>
        SendAsync<PickupDto>(HttpMethod.Post, "api/scrap/pickups", request, cancellationToken);

    public Task<ApiResult<IReadOnlyList<PickupDto>>> GetMyPickupsAsync(CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<PickupDto>>(HttpMethod.Get, "api/scrap/pickups/mine", null, cancellationToken);

    public Task<ApiResult<IReadOnlyList<PickupDto>>> GetOpenPickupsAsync(string provinceCode, CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<PickupDto>>(HttpMethod.Get, $"api/scrap/pickups/open?province={Uri.EscapeDataString(provinceCode)}", null, cancellationToken);

    public Task<ApiResult<PickupDto>> PickupActionAsync(Guid id, string action, CancellationToken cancellationToken) =>
        SendAsync<PickupDto>(HttpMethod.Post, $"api/scrap/pickups/{id}/{Uri.EscapeDataString(action)}", null, cancellationToken);

    public async Task<ApiResult<string>> ExportMyDataAsync(CancellationToken cancellationToken)
    {
        var result = await SendAsync<JsonElement>(HttpMethod.Get, "api/me/export", null, cancellationToken);
        return result.IsSuccess
            ? ApiResult<string>.Ok(JsonSerializer.Serialize(result.Value, new JsonSerializerOptions { WriteIndented = true }))
            : ApiResult<string>.Fail(result.Outcome, result.Problem);
    }

    public async Task<ApiResult<bool>> RequestAccountDeletionAsync(CancellationToken cancellationToken)
    {
        var result = await SendAsync<JsonElement?>(HttpMethod.Delete, "api/me", null, cancellationToken);
        return result.IsSuccess ? ApiResult<bool>.Ok(true) : ApiResult<bool>.Fail(result.Outcome, result.Problem);
    }
}
