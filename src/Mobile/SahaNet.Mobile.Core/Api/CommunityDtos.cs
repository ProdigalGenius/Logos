namespace SahaNet.Mobile.Core.Api;

// Pazar, sohbet, hurda ve hesap sözleşmelerinin mobil kopyaları.

public sealed record CategoryDto(string Key, string NicheKey, string Name, string DefaultUnit, IReadOnlyList<string> AllowedTypes);

public sealed record OfferDto(Guid Id, Guid ListingId, Guid BuyerId, decimal UnitPrice, decimal Quantity, decimal Total, string? Message, string Status, DateTimeOffset CreatedAt);

public sealed record ListingDto(
    Guid Id,
    Guid SellerId,
    string? SellerName,
    string NicheKey,
    string CategoryKey,
    string Title,
    string? Description,
    string Type,
    string Status,
    decimal UnitPrice,
    decimal Quantity,
    decimal RemainingQuantity,
    string Unit,
    string? RegionCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    decimal? HighestBid,
    int OfferCount,
    IReadOnlyList<OfferDto> Offers)
{
    public string PriceText => $"{UnitPrice:0.##} ₺ / {Unit}";
}

public sealed record ListingPageDto(IReadOnlyList<ListingDto> Items, int Page, int PageSize, int Total);

public sealed record CreateListingRequest(
    string CategoryKey,
    string Title,
    string? Description,
    string Type,
    decimal UnitPrice,
    decimal Quantity,
    string? Unit,
    string? RegionCode,
    IReadOnlyDictionary<string, string>? Attributes,
    DateTimeOffset? ExpiresAt,
    decimal? MinBidIncrement,
    decimal? ReservePrice);

public sealed record ParticipantDto(Guid UserId, string? DisplayName);

public sealed record MessageDto(Guid Id, Guid ConversationId, Guid SenderId, string Kind, string? Text, string? MediaUrl, DateTimeOffset SentAt);

public sealed record ConversationDto(Guid Id, Guid? ListingId, IReadOnlyList<ParticipantDto> Participants, MessageDto? LastMessage, int UnreadCount, DateTimeOffset LastMessageAt);

public sealed record MaterialDto(string Code, string Name, string Group);

public sealed record ScrapPriceDto(DateOnly Date, string MaterialCode, string MaterialName, string ProvinceCode, string? ProvinceName, decimal Median, decimal Min, decimal Max, int Reports);

public sealed record PickupDto(
    Guid Id,
    string ProvinceCode,
    string? ProvinceName,
    string District,
    string? Address,
    IReadOnlyList<string> Materials,
    decimal EstimatedKg,
    string? Notes,
    string Status,
    bool IsMine,
    bool IsMyJob,
    DateTimeOffset CreatedAt)
{
    public string MaterialsText => string.Join(", ", Materials);
}

public sealed record CreatePickupRequest(string ProvinceCode, string District, string Address, IReadOnlyList<string> Materials, decimal EstimatedKg, string? Notes);
