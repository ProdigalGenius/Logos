using SahaNet.Domain.Common;

namespace SahaNet.Domain.Market;

public enum OfferStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Withdrawn = 3,
    Outbid = 4,

    /// <summary>Kabul edildi ama ödeme süresinde ödenmedi; miktar ilana geri döndü.</summary>
    Cancelled = 5,
}

/// <summary>İlana verilen teklif / ön sipariş / ihale teklifi. <see cref="Listing"/> aggregate'inin parçasıdır.</summary>
public sealed class Offer : Entity<Guid>
{
    public const int MessageMaxLength = 500;

    internal Offer(Guid id, Guid listingId, Guid buyerId, decimal unitPrice, decimal quantity, string? message, DateTimeOffset now)
        : base(id)
    {
        ListingId = listingId;
        BuyerId = buyerId;
        UnitPrice = unitPrice;
        Quantity = quantity;
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        Status = OfferStatus.Pending;
        CreatedAt = now;
    }

    // EF Core
    private Offer()
        : base(Guid.Empty)
    {
    }

    public Guid ListingId { get; private set; }

    public Guid BuyerId { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal Quantity { get; private set; }

    public decimal Total => decimal.Round(UnitPrice * Quantity, 2);

    public string? Message { get; private set; }

    public OfferStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    internal void Accept(DateTimeOffset now)
    {
        Status = OfferStatus.Accepted;
        AcceptedAt = now;
    }

    internal void Reject() => Status = OfferStatus.Rejected;

    internal void Withdraw() => Status = OfferStatus.Withdrawn;

    internal void MarkOutbid() => Status = OfferStatus.Outbid;

    internal void Cancel() => Status = OfferStatus.Cancelled;
}
