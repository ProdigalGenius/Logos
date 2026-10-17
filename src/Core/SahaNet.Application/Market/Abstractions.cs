using SahaNet.Domain.Market;

namespace SahaNet.Application.Market;

public sealed record ListingSearch(
    string? NicheKey,
    string? CategoryKey,
    string? RegionCode,
    ListingType? Type,
    string? Text,
    int Page,
    int PageSize);

public interface IListingRepository
{
    Task<Listing?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Listing?> GetByOfferIdAsync(Guid offerId, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Listing> Items, int Total)> SearchAsync(ListingSearch search, DateTimeOffset now, CancellationToken cancellationToken);

    Task<IReadOnlyList<Listing>> GetBySellerAsync(Guid sellerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Listing>> GetWithOffersByBuyerAsync(Guid buyerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Listing>> GetDueForClosingAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken);

    void Add(Listing listing);

    /// <summary>Yüklü ilana eklenen teklifi açıkça "yeni" olarak işaretler.</summary>
    void AddOffer(Offer offer);
}

public sealed record ConversationSummary(Conversation Conversation, ChatMessage? LastMessage, int UnreadCount);

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Conversation?> FindDirectAsync(Guid? listingId, Guid first, Guid second, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConversationSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(Guid conversationId, DateTimeOffset? before, int limit, CancellationToken cancellationToken);

    Task<ChatMessage?> GetMessageAsync(Guid messageId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatMessage>> GetMessagesBySenderAsync(Guid senderId, int limit, CancellationToken cancellationToken);

    Task<int> CountRecentMessagesAsync(Guid senderId, DateTimeOffset since, CancellationToken cancellationToken);

    void Add(Conversation conversation);

    void AddMessage(ChatMessage message);
}

/// <summary>Gerçek zamanlı iletim (SignalR). Api katmanında uygulanır.</summary>
public interface IChatRealtime
{
    Task MessageSentAsync(IReadOnlyCollection<Guid> recipientIds, MessageResponse message, CancellationToken cancellationToken);
}

public sealed record MessageResponse(Guid Id, Guid ConversationId, Guid SenderId, string Kind, string? Text, string? MediaUrl, DateTimeOffset SentAt);
