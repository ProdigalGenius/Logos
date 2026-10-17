using SahaNet.Domain.Common;

namespace SahaNet.Domain.Market;

public enum ListingType
{
    /// <summary>Sabit fiyat; alıcılar miktar belirterek ön sipariş/rezervasyon ister.</summary>
    FixedPrice = 0,

    /// <summary>Teklif toplama; satıcı uygun teklifi kabul eder.</summary>
    OfferBased = 1,

    /// <summary>Süreli ihale; süre sonunda en yüksek teklif kazanır.</summary>
    Auction = 2,
}

public enum ListingStatus
{
    Active = 0,
    Sold = 1,
    Expired = 2,
    Cancelled = 3,

    /// <summary>Moderasyon kararıyla yayından kaldırıldı.</summary>
    Removed = 4,
}

public sealed record OfferPlaced(Guid ListingId, Guid OfferId, Guid SellerId, Guid BuyerId) : Events.DomainEvent;

public sealed record OfferAccepted(Guid ListingId, Guid OfferId, Guid SellerId, Guid BuyerId) : Events.DomainEvent;

public sealed record OfferRejected(Guid ListingId, Guid OfferId, Guid BuyerId) : Events.DomainEvent;

public sealed record BidOutbid(Guid ListingId, Guid OfferId, Guid BuyerId, decimal NewHighest) : Events.DomainEvent;

public sealed record ListingClosed(Guid ListingId, Guid SellerId, ListingStatus Status) : Events.DomainEvent;

/// <summary>
/// Pazar ilanı. Para birimi TRY; fiyatlar birim (kg/adet) başınadır. Kategoriye özel alanlar
/// <see cref="Attributes"/> içinde, modülün tanımladığı şemaya göre doğrulanmış olarak tutulur.
/// </summary>
public sealed class Listing : AggregateRoot<Guid>
{
    public const int TitleMaxLength = 120;
    public const int DescriptionMaxLength = 4000;

    /// <summary>İhalenin son dakikalarında gelen teklif süreyi uzatır (son dakika kapışmasına karşı).</summary>
    public static readonly TimeSpan AntiSnipingWindow = TimeSpan.FromMinutes(2);

    private readonly List<Offer> _offers = [];

    private Listing(Guid id)
        : base(id)
    {
        NicheKey = null!;
        CategoryKey = null!;
        Title = null!;
        Unit = null!;
        Attributes = [];
    }

    // EF Core
    private Listing()
        : this(Guid.Empty)
    {
    }

    public Guid SellerId { get; private set; }

    public string NicheKey { get; private set; }

    public string CategoryKey { get; private set; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    public ListingType Type { get; private set; }

    public ListingStatus Status { get; private set; }

    /// <summary>Sabit fiyatta satış fiyatı; teklif/ihalede başlangıç (alt) fiyat.</summary>
    public decimal UnitPrice { get; private set; }

    public decimal Quantity { get; private set; }

    public decimal RemainingQuantity { get; private set; }

    /// <summary>"kg", "adet" ...</summary>
    public string Unit { get; private set; }

    public string? RegionCode { get; private set; }

    public Dictionary<string, string> Attributes { get; private set; }

    public decimal? MinBidIncrement { get; private set; }

    /// <summary>İhalede altında satış yapılmayacak gizli taban fiyat.</summary>
    public decimal? ReservePrice { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    /// Her teklif/karar ilan satırını günceller; böylece satır sürümü (xmin) eşzamanlı teklifleri
    /// sıraya sokar (iki kişi aynı anda "en yüksek" olamaz).
    /// </summary>
    public DateTimeOffset LastActivityAt { get; private set; }

    public string? ModerationNote { get; private set; }

    public IReadOnlyCollection<Offer> Offers => _offers.AsReadOnly();

    public Offer? HighestBid => _offers
        .Where(o => o.Status is OfferStatus.Pending or OfferStatus.Accepted)
        .OrderByDescending(o => o.UnitPrice)
        .ThenBy(o => o.CreatedAt)
        .FirstOrDefault();

    public static Listing Create(
        Guid sellerId,
        string nicheKey,
        string categoryKey,
        string title,
        string? description,
        ListingType type,
        decimal unitPrice,
        decimal quantity,
        string unit,
        string? regionCode,
        IReadOnlyDictionary<string, string> attributes,
        DateTimeOffset expiresAt,
        decimal? minBidIncrement,
        decimal? reservePrice,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentNullException.ThrowIfNull(attributes);
        ArgumentOutOfRangeException.ThrowIfNegative(unitPrice);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        if (expiresAt <= now)
        {
            throw new ArgumentException("Bitiş zamanı gelecekte olmalı.", nameof(expiresAt));
        }

        return new Listing(Guid.NewGuid())
        {
            SellerId = sellerId,
            NicheKey = nicheKey,
            CategoryKey = categoryKey,
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Type = type,
            Status = ListingStatus.Active,
            UnitPrice = unitPrice,
            Quantity = quantity,
            RemainingQuantity = quantity,
            Unit = unit,
            RegionCode = regionCode,
            Attributes = new Dictionary<string, string>(attributes, StringComparer.Ordinal),
            MinBidIncrement = type == ListingType.Auction ? Math.Max(minBidIncrement ?? 1m, 0.01m) : null,
            ReservePrice = type == ListingType.Auction ? reservePrice : null,
            CreatedAt = now,
            ExpiresAt = expiresAt,
            LastActivityAt = now,
        };
    }

    public bool IsOpen(DateTimeOffset now) => Status == ListingStatus.Active && now < ExpiresAt;

    /// <summary>Teklif / ön sipariş / ihale teklifi.</summary>
    public Result<Offer> PlaceOffer(Guid buyerId, decimal unitPrice, decimal quantity, string? message, DateTimeOffset now)
    {
        if (!IsOpen(now))
        {
            return Result.Failure<Offer>(Type == ListingType.Auction && Status == ListingStatus.Active ? MarketErrors.AuctionEnded : MarketErrors.ListingNotActive);
        }

        if (buyerId == SellerId)
        {
            return Result.Failure<Offer>(MarketErrors.OwnListing);
        }

        switch (Type)
        {
            case ListingType.FixedPrice when unitPrice != UnitPrice:
                return Result.Failure<Offer>(MarketErrors.PriceMismatch);
            case ListingType.Auction:
                // İhale tüm partiyi kapsar.
                quantity = RemainingQuantity;
                var minimum = HighestBid is { } top ? top.UnitPrice + MinBidIncrement!.Value : UnitPrice;
                if (unitPrice < minimum)
                {
                    return Result.Failure<Offer>(MarketErrors.BidTooLow);
                }

                break;
        }

        if (quantity <= 0 || quantity > RemainingQuantity)
        {
            return Result.Failure<Offer>(MarketErrors.QuantityUnavailable);
        }

        LastActivityAt = now;
        var previousTop = Type == ListingType.Auction ? HighestBid : null;
        var offer = new Offer(Guid.NewGuid(), Id, buyerId, unitPrice, quantity, message, now);
        _offers.Add(offer);
        Raise(new OfferPlaced(Id, offer.Id, SellerId, buyerId));

        if (previousTop is not null)
        {
            previousTop.MarkOutbid();
            Raise(new BidOutbid(Id, previousTop.Id, previousTop.BuyerId, unitPrice));
        }

        if (Type == ListingType.Auction && ExpiresAt - now < AntiSnipingWindow)
        {
            ExpiresAt = now + AntiSnipingWindow;
        }

        return Result.Success(offer);
    }

    public Result AcceptOffer(Guid sellerId, Guid offerId, DateTimeOffset now)
    {
        if (sellerId != SellerId)
        {
            return Result.Failure(MarketErrors.NotSeller);
        }

        if (Type == ListingType.Auction)
        {
            return Result.Failure(MarketErrors.AuctionAcceptNotAllowed);
        }

        if (!IsOpen(now))
        {
            return Result.Failure(MarketErrors.ListingNotActive);
        }

        var offer = _offers.FirstOrDefault(o => o.Id == offerId);
        if (offer is null)
        {
            return Result.Failure(MarketErrors.OfferNotFound);
        }

        if (offer.Status != OfferStatus.Pending)
        {
            return Result.Failure(MarketErrors.OfferNotPending);
        }

        if (offer.Quantity > RemainingQuantity)
        {
            return Result.Failure(MarketErrors.QuantityUnavailable);
        }

        Accept(offer, now);
        return Result.Success();
    }

    public Result RejectOffer(Guid sellerId, Guid offerId)
    {
        if (sellerId != SellerId)
        {
            return Result.Failure(MarketErrors.NotSeller);
        }

        var offer = _offers.FirstOrDefault(o => o.Id == offerId);
        if (offer is null)
        {
            return Result.Failure(MarketErrors.OfferNotFound);
        }

        if (offer.Status != OfferStatus.Pending || Type == ListingType.Auction)
        {
            return Result.Failure(MarketErrors.OfferNotPending);
        }

        offer.Reject();
        LastActivityAt = DateTimeOffset.UtcNow;
        Raise(new OfferRejected(Id, offer.Id, offer.BuyerId));
        return Result.Success();
    }

    public Result WithdrawOffer(Guid buyerId, Guid offerId)
    {
        var offer = _offers.FirstOrDefault(o => o.Id == offerId);
        if (offer is null)
        {
            return Result.Failure(MarketErrors.OfferNotFound);
        }

        if (offer.BuyerId != buyerId)
        {
            return Result.Failure(MarketErrors.NotBuyer);
        }

        // İhalede verilen teklif geri çekilemez (bağlayıcıdır).
        if (offer.Status != OfferStatus.Pending || Type == ListingType.Auction)
        {
            return Result.Failure(MarketErrors.OfferNotPending);
        }

        offer.Withdraw();
        LastActivityAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result Cancel(Guid sellerId)
    {
        if (sellerId != SellerId)
        {
            return Result.Failure(MarketErrors.NotSeller);
        }

        if (Status != ListingStatus.Active)
        {
            return Result.Failure(MarketErrors.ListingNotActive);
        }

        // Kabul edilmiş ön siparişler korunur; yalnızca bekleyenler kapanır.
        Close(ListingStatus.Cancelled);
        return Result.Success();
    }

    /// <summary>Moderatör ilanı yayından kaldırır; bekleyen teklifler reddedilir.</summary>
    public Result RemoveByModerator(string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status == ListingStatus.Removed)
        {
            return Result.Failure(MarketErrors.ListingNotActive);
        }

        ModerationNote = reason.Trim();
        LastActivityAt = now;
        Close(ListingStatus.Removed);
        return Result.Success();
    }

    /// <summary>
    /// Kabul edilmiş ama ödenmemiş teklifi iptal eder: miktar stoğa döner ve ilan süresi dolmadıysa
    /// tekrar satışa açılır (ihale hariç; ihale kazananı ödemezse ilan sona erer).
    /// </summary>
    public Result CancelAcceptedOffer(Guid offerId, DateTimeOffset now)
    {
        var offer = _offers.FirstOrDefault(o => o.Id == offerId);
        if (offer is null)
        {
            return Result.Failure(MarketErrors.OfferNotFound);
        }

        if (offer.Status != OfferStatus.Accepted)
        {
            return Result.Failure(MarketErrors.OfferNotPending);
        }

        offer.Cancel();
        LastActivityAt = now;
        if (Type == ListingType.Auction)
        {
            Status = ListingStatus.Expired;
            return Result.Success();
        }

        RemainingQuantity += offer.Quantity;
        if (Status == ListingStatus.Sold && now < ExpiresAt)
        {
            Status = ListingStatus.Active;
        }

        return Result.Success();
    }

    /// <summary>Süresi dolan ilanı kapatır; ihalede en yüksek teklif taban fiyatı geçiyorsa kazanır.</summary>
    public bool CloseIfDue(DateTimeOffset now)
    {
        if (Status != ListingStatus.Active || now < ExpiresAt)
        {
            return false;
        }

        if (Type == ListingType.Auction && HighestBid is { Status: OfferStatus.Pending } winner && (ReservePrice is null || winner.UnitPrice >= ReservePrice))
        {
            Accept(winner, now);
            return true;
        }

        Close(_offers.Any(o => o.Status == OfferStatus.Accepted) ? ListingStatus.Sold : ListingStatus.Expired);
        return true;
    }

    private void Accept(Offer offer, DateTimeOffset now)
    {
        offer.Accept(now);
        LastActivityAt = now;
        RemainingQuantity -= offer.Quantity;
        Raise(new OfferAccepted(Id, offer.Id, SellerId, offer.BuyerId));

        if (RemainingQuantity <= 0 || Type == ListingType.Auction)
        {
            RemainingQuantity = 0;
            Close(ListingStatus.Sold);
        }
    }

    private void Close(ListingStatus status)
    {
        Status = status;
        foreach (var pending in _offers.Where(o => o.Status == OfferStatus.Pending))
        {
            pending.Reject();
            Raise(new OfferRejected(Id, pending.Id, pending.BuyerId));
        }

        Raise(new ListingClosed(Id, SellerId, status));
    }
}
