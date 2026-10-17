using SahaNet.Domain.Common;

namespace SahaNet.Domain.Market;

public static class MarketErrors
{
    public static readonly Error ListingNotFound = new("market.listing_not_found", "İlan bulunamadı.", ErrorType.NotFound);
    public static readonly Error OfferNotFound = new("market.offer_not_found", "Teklif bulunamadı.", ErrorType.NotFound);
    public static readonly Error ListingNotActive = new("market.listing_not_active", "İlan artık aktif değil.", ErrorType.Conflict);
    public static readonly Error OwnListing = new("market.own_listing", "Kendi ilanınıza teklif veremezsiniz.", ErrorType.Validation);
    public static readonly Error QuantityUnavailable = new("market.quantity_unavailable", "İstenen miktar mevcut değil.", ErrorType.Conflict);
    public static readonly Error PriceMismatch = new("market.price_mismatch", "Sabit fiyatlı ilanda fiyat değiştirilemez.", ErrorType.Validation);
    public static readonly Error BidTooLow = new("market.bid_too_low", "Teklif, en yüksek tekliften yeterince yüksek değil.", ErrorType.Conflict);
    public static readonly Error AuctionEnded = new("market.auction_ended", "İhale süresi doldu.", ErrorType.Conflict);
    public static readonly Error AuctionAcceptNotAllowed = new("market.auction_accept", "İhale sonunda en yüksek teklif otomatik kazanır.", ErrorType.Validation);
    public static readonly Error OfferNotPending = new("market.offer_not_pending", "Teklif artık beklemede değil.", ErrorType.Conflict);
    public static readonly Error NotSeller = new("market.not_seller", "Bu işlem yalnızca ilan sahibine açık.", ErrorType.Forbidden);
    public static readonly Error NotBuyer = new("market.not_buyer", "Bu işlem yalnızca teklif sahibine açık.", ErrorType.Forbidden);
    public static readonly Error CategoryUnknown = new("market.category_unknown", "Kategori bulunamadı.", ErrorType.Validation);
    public static readonly Error OnboardingRequired = new("market.onboarding_required", "İlan vermek için önce kurulumu tamamlayın.", ErrorType.Forbidden);
    public static readonly Error ConversationNotFound = new("market.conversation_not_found", "Sohbet bulunamadı.", ErrorType.NotFound);

    public static Error AttributeInvalid(string message) => new("market.attribute_invalid", message, ErrorType.Validation);
}
