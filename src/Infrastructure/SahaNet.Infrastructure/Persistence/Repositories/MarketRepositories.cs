using Microsoft.EntityFrameworkCore;
using SahaNet.Application.Market;
using SahaNet.Domain.Market;

namespace SahaNet.Infrastructure.Persistence.Repositories;

internal sealed class ListingRepository(SahaNetDbContext db) : IListingRepository
{
    public Task<Listing?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Listings.Include(l => l.Offers).FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public Task<Listing?> GetByOfferIdAsync(Guid offerId, CancellationToken cancellationToken) =>
        db.Listings.Include(l => l.Offers).FirstOrDefaultAsync(l => l.Offers.Any(o => o.Id == offerId), cancellationToken);

    public async Task<(IReadOnlyList<Listing> Items, int Total)> SearchAsync(ListingSearch search, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        var query = db.Listings.AsNoTracking().Where(l => l.Status == ListingStatus.Active && l.ExpiresAt > now);
        if (search.NicheKey is not null)
        {
            query = query.Where(l => l.NicheKey == search.NicheKey);
        }

        if (search.CategoryKey is not null)
        {
            query = query.Where(l => l.CategoryKey == search.CategoryKey);
        }

        if (search.RegionCode is not null)
        {
            query = query.Where(l => l.RegionCode == search.RegionCode);
        }

        if (search.Type is { } type)
        {
            query = query.Where(l => l.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(search.Text))
        {
            var pattern = "%" + search.Text.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(l => EF.Functions.ILike(l.Title, pattern) || (l.Description != null && EF.Functions.ILike(l.Description, pattern)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(l => l.Offers)
            .OrderByDescending(l => l.CreatedAt)
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<Listing>> GetBySellerAsync(Guid sellerId, CancellationToken cancellationToken) =>
        await db.Listings.AsNoTracking().Include(l => l.Offers)
            .Where(l => l.SellerId == sellerId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Listing>> GetWithOffersByBuyerAsync(Guid buyerId, CancellationToken cancellationToken) =>
        await db.Listings.AsNoTracking().Include(l => l.Offers)
            .Where(l => l.Offers.Any(o => o.BuyerId == buyerId))
            .OrderByDescending(l => l.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Listing>> GetDueForClosingAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken) =>
        await db.Listings.Include(l => l.Offers)
            .Where(l => l.Status == ListingStatus.Active && l.ExpiresAt <= now)
            .OrderBy(l => l.ExpiresAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public void Add(Listing listing) => db.Listings.Add(listing);

    public void AddOffer(Offer offer) => db.Offers.Add(offer);
}

internal sealed class ConversationRepository(SahaNetDbContext db) : IConversationRepository
{
    public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Conversations.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Conversation?> FindDirectAsync(Guid? listingId, Guid first, Guid second, CancellationToken cancellationToken) =>
        db.Conversations.FirstOrDefaultAsync(
            c => c.ListingId == listingId &&
                 c.Participants.Count == 2 &&
                 c.Participants.Any(p => p.UserId == first) &&
                 c.Participants.Any(p => p.UserId == second),
            cancellationToken);

    public async Task<IReadOnlyList<ConversationSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var conversations = await db.Conversations.AsNoTracking()
            .Where(c => c.Participants.Any(p => p.UserId == userId))
            .OrderByDescending(c => c.LastMessageAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var result = new List<ConversationSummary>(conversations.Count);
        foreach (var conversation in conversations)
        {
            var lastRead = conversation.Participants.First(p => p.UserId == userId).LastReadAt;
            var last = await db.Messages.AsNoTracking()
                .Where(m => m.ConversationId == conversation.Id)
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefaultAsync(cancellationToken);
            var unread = await db.Messages.CountAsync(
                m => m.ConversationId == conversation.Id && m.SenderId != userId && m.SentAt > lastRead,
                cancellationToken);
            result.Add(new ConversationSummary(conversation, last, unread));
        }

        return result;
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(Guid conversationId, DateTimeOffset? before, int limit, CancellationToken cancellationToken)
    {
        var query = db.Messages.AsNoTracking().Where(m => m.ConversationId == conversationId);
        if (before is { } b)
        {
            query = query.Where(m => m.SentAt < b);
        }

        var page = await query.OrderByDescending(m => m.SentAt).Take(limit).ToListAsync(cancellationToken);
        page.Reverse();
        return page;
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesBySenderAsync(Guid senderId, int limit, CancellationToken cancellationToken) =>
        await db.Messages.AsNoTracking().Where(m => m.SenderId == senderId).OrderBy(m => m.SentAt).Take(limit).ToListAsync(cancellationToken);

    public Task<int> CountRecentMessagesAsync(Guid senderId, DateTimeOffset since, CancellationToken cancellationToken) =>
        db.Messages.CountAsync(m => m.SenderId == senderId && m.SentAt >= since, cancellationToken);

    public void Add(Conversation conversation) => db.Conversations.Add(conversation);

    public void AddMessage(ChatMessage message) => db.Messages.Add(message);
}
