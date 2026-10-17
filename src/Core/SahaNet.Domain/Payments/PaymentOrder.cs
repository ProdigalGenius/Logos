using SahaNet.Domain.Common;
using SahaNet.Domain.Events;

namespace SahaNet.Domain.Payments;

public enum PaymentStatus
{
    /// <summary>Sipariş oluştu, alıcının ödemesi bekleniyor.</summary>
    AwaitingPayment = 0,

    /// <summary>Ödeme alındı, para emanette (satıcıya geçmedi).</summary>
    Held = 1,

    /// <summary>Satıcı teslim ettiğini bildirdi; alıcı onayı veya itiraz süresi bekleniyor.</summary>
    Delivered = 2,

    /// <summary>Alıcı itiraz etti; yönetici kararı bekleniyor.</summary>
    Disputed = 3,

    /// <summary>Para satıcıya geçti (komisyon düşülerek).</summary>
    Released = 4,

    /// <summary>Para alıcıya iade edildi.</summary>
    Refunded = 5,

    /// <summary>Süresinde ödenmedi.</summary>
    Cancelled = 6,
}

public enum DisputeDecision
{
    ReleaseToSeller = 0,
    RefundBuyer = 1,
}

public sealed record PaymentHeld(Guid OrderId, Guid BuyerId, Guid SellerId) : DomainEvent;

public sealed record PaymentDeliveryReported(Guid OrderId, Guid BuyerId, DateTimeOffset AutoReleaseAt) : DomainEvent;

public sealed record PaymentReleased(Guid OrderId, Guid SellerId, decimal Payout, decimal Commission) : DomainEvent;

public sealed record PaymentRefunded(Guid OrderId, Guid BuyerId, decimal Amount) : DomainEvent;

public sealed record PaymentDisputed(Guid OrderId, Guid SellerId, string Reason) : DomainEvent;

public sealed record PaymentCancelled(Guid OrderId, Guid ListingId, Guid OfferId) : DomainEvent;

public static class PaymentErrors
{
    public static readonly Error OrderNotFound = new("payment.order_not_found", "Ödeme kaydı bulunamadı.", ErrorType.NotFound);
    public static readonly Error InvalidState = new("payment.invalid_state", "Bu işlem ödemenin mevcut durumunda yapılamaz.", ErrorType.Conflict);
    public static readonly Error NotBuyer = new("payment.not_buyer", "Bu işlem yalnızca alıcıya açık.", ErrorType.Forbidden);
    public static readonly Error NotSeller = new("payment.not_seller", "Bu işlem yalnızca satıcıya açık.", ErrorType.Forbidden);
    public static readonly Error OfferNotAccepted = new("payment.offer_not_accepted", "Yalnızca kabul edilmiş teklif için ödeme yapılabilir.", ErrorType.Conflict);
    public static readonly Error DisputeWindowClosed = new("payment.dispute_window_closed", "İtiraz süresi doldu.", ErrorType.Conflict);
    public static readonly Error AmountMismatch = new("payment.amount_mismatch", "Ödenen tutar sipariş tutarıyla uyuşmuyor.", ErrorType.Conflict);
    public static readonly Error GatewayFailed = new("payment.gateway_failed", "Ödeme sağlayıcısı işlemi onaylamadı.", ErrorType.Validation);
}

/// <summary>
/// Emanet (escrow) ödeme. Alıcı öder → para emanette bekler → teslim onayıyla (veya itiraz süresi
/// dolunca) satıcıya komisyon düşülerek geçer. İtirazda yönetici serbest bırakır ya da iade eder.
/// Mali kayıt olduğu için kullanıcı silinse de saklanır (VUK saklama yükümlülüğü).
/// </summary>
public sealed class PaymentOrder : AggregateRoot<Guid>
{
    public const int ReasonMaxLength = 1000;

    private PaymentOrder(Guid id)
        : base(id)
    {
        Currency = null!;
    }

    // EF Core
    private PaymentOrder()
        : this(Guid.Empty)
    {
    }

    public Guid ListingId { get; private set; }

    public Guid OfferId { get; private set; }

    public Guid BuyerId { get; private set; }

    public Guid SellerId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; }

    /// <summary>Sipariş anında sabitlenen komisyon oranı (sonradan oran değişse de etkilenmez).</summary>
    public decimal CommissionRate { get; private set; }

    public decimal Commission { get; private set; }

    public decimal SellerPayout => Amount - Commission;

    public PaymentStatus Status { get; private set; }

    public string? ProviderPaymentId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset PayBy { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }

    public DateTimeOffset? AutoReleaseAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public string? DisputeReason { get; private set; }

    public DateTimeOffset? DisputedAt { get; private set; }

    public DisputeDecision? Decision { get; private set; }

    public string? DecisionNote { get; private set; }

    public Guid? DecidedBy { get; private set; }

    public static PaymentOrder Create(
        Guid listingId,
        Guid offerId,
        Guid buyerId,
        Guid sellerId,
        decimal amount,
        decimal commissionRate,
        decimal minimumCommission,
        TimeSpan paymentWindow,
        DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        ArgumentOutOfRangeException.ThrowIfNegative(commissionRate);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(commissionRate, 1m);

        return new PaymentOrder(Guid.NewGuid())
        {
            ListingId = listingId,
            OfferId = offerId,
            BuyerId = buyerId,
            SellerId = sellerId,
            Amount = decimal.Round(amount, 2),
            Currency = "TRY",
            CommissionRate = commissionRate,
            Commission = CalculateCommission(amount, commissionRate, minimumCommission),
            Status = PaymentStatus.AwaitingPayment,
            CreatedAt = now,
            PayBy = now + paymentWindow,
        };
    }

    /// <summary>Komisyon = max(oran × tutar, asgari ücret), tutarı aşamaz; kuruşa yuvarlanır (banker değil, yukarı yarım).</summary>
    public static decimal CalculateCommission(decimal amount, decimal rate, decimal minimum) =>
        Math.Min(decimal.Round(amount, 2), Math.Max(decimal.Round(amount * rate, 2, MidpointRounding.AwayFromZero), minimum));

    public Result MarkPaid(string providerPaymentId, decimal paidAmount, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerPaymentId);

        // Sağlayıcı geri çağrısı tekrarlanabilir: aynı ödeme ikinci kez işlenmez.
        if (Status != PaymentStatus.AwaitingPayment)
        {
            return ProviderPaymentId == providerPaymentId ? Result.Success() : Result.Failure(PaymentErrors.InvalidState);
        }

        if (decimal.Round(paidAmount, 2) != Amount)
        {
            return Result.Failure(PaymentErrors.AmountMismatch);
        }

        ProviderPaymentId = providerPaymentId;
        Status = PaymentStatus.Held;
        PaidAt = now;
        Raise(new PaymentHeld(Id, BuyerId, SellerId));
        return Result.Success();
    }

    public Result ReportDelivered(Guid sellerId, TimeSpan disputeWindow, DateTimeOffset now)
    {
        if (sellerId != SellerId)
        {
            return Result.Failure(PaymentErrors.NotSeller);
        }

        if (Status != PaymentStatus.Held)
        {
            return Result.Failure(PaymentErrors.InvalidState);
        }

        Status = PaymentStatus.Delivered;
        DeliveredAt = now;
        AutoReleaseAt = now + disputeWindow;
        Raise(new PaymentDeliveryReported(Id, BuyerId, AutoReleaseAt.Value));
        return Result.Success();
    }

    /// <summary>Alıcı teslim aldığını onaylar; para satıcıya geçer.</summary>
    public Result ConfirmReceipt(Guid buyerId, DateTimeOffset now)
    {
        if (buyerId != BuyerId)
        {
            return Result.Failure(PaymentErrors.NotBuyer);
        }

        if (Status is not (PaymentStatus.Held or PaymentStatus.Delivered))
        {
            return Result.Failure(PaymentErrors.InvalidState);
        }

        Release(now);
        return Result.Success();
    }

    public Result OpenDispute(Guid buyerId, string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (buyerId != BuyerId)
        {
            return Result.Failure(PaymentErrors.NotBuyer);
        }

        if (Status is not (PaymentStatus.Held or PaymentStatus.Delivered))
        {
            return Result.Failure(PaymentErrors.InvalidState);
        }

        if (AutoReleaseAt is { } deadline && now >= deadline)
        {
            return Result.Failure(PaymentErrors.DisputeWindowClosed);
        }

        Status = PaymentStatus.Disputed;
        DisputeReason = reason.Trim();
        DisputedAt = now;
        AutoReleaseAt = null;
        Raise(new PaymentDisputed(Id, SellerId, DisputeReason));
        return Result.Success();
    }

    public Result Resolve(Guid adminId, DisputeDecision decision, string? note, DateTimeOffset now)
    {
        if (Status != PaymentStatus.Disputed)
        {
            return Result.Failure(PaymentErrors.InvalidState);
        }

        Decision = decision;
        DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        DecidedBy = adminId;

        if (decision == DisputeDecision.ReleaseToSeller)
        {
            Release(now);
        }
        else
        {
            // İadede komisyon alınmaz.
            Commission = 0;
            Status = PaymentStatus.Refunded;
            ClosedAt = now;
            Raise(new PaymentRefunded(Id, BuyerId, Amount));
        }

        return Result.Success();
    }

    /// <summary>İtiraz süresi dolduysa otomatik serbest bırakır.</summary>
    public bool AutoReleaseIfDue(DateTimeOffset now)
    {
        if (Status != PaymentStatus.Delivered || AutoReleaseAt is not { } at || now < at)
        {
            return false;
        }

        Release(now);
        return true;
    }

    /// <summary>Ödeme süresi dolduysa iptal eder (ilan rezervasyonu geri açılır).</summary>
    public bool CancelIfUnpaid(DateTimeOffset now)
    {
        if (Status != PaymentStatus.AwaitingPayment || now < PayBy)
        {
            return false;
        }

        Status = PaymentStatus.Cancelled;
        ClosedAt = now;
        Raise(new PaymentCancelled(Id, ListingId, OfferId));
        return true;
    }

    private void Release(DateTimeOffset now)
    {
        Status = PaymentStatus.Released;
        ClosedAt = now;
        AutoReleaseAt = null;
        Raise(new PaymentReleased(Id, SellerId, SellerPayout, Commission));
    }
}
