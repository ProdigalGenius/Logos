using SahaNet.Application.Modules;

namespace SahaNet.Application.Market;

/// <summary>KVKK dışa aktarım: ilanlarım, tekliflerim ve gönderdiğim mesajlar.</summary>
internal sealed class MarketPersonalDataSection(IListingRepository listings, IConversationRepository conversations) : IPersonalDataSection
{
    public string Name => "market";

    public async Task<object?> ExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        var mine = await listings.GetBySellerAsync(userId, cancellationToken);
        var offered = await listings.GetWithOffersByBuyerAsync(userId, cancellationToken);
        var sent = await conversations.GetMessagesBySenderAsync(userId, 5000, cancellationToken);

        return new
        {
            Listings = mine.Select(l => new { l.Id, l.Title, l.Description, Type = l.Type.ToString(), Status = l.Status.ToString(), l.UnitPrice, l.Quantity, l.Unit, l.CreatedAt }),
            Offers = offered.SelectMany(l => l.Offers.Where(o => o.BuyerId == userId)
                .Select(o => new { ListingTitle = l.Title, o.UnitPrice, o.Quantity, o.Message, Status = o.Status.ToString(), o.CreatedAt })),
            Messages = sent.Select(m => new { m.ConversationId, Kind = m.Kind.ToString(), m.Text, m.MediaUrl, m.SentAt }),
        };
    }
}
