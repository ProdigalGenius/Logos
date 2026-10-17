using System.ComponentModel.DataAnnotations;
using SahaNet.Domain.Payments;

namespace SahaNet.Application.Payments;

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";

    /// <summary>Kabul edilen teklifin ödenmesi için süre; dolunca rezervasyon geri açılır.</summary>
    public TimeSpan PaymentWindow { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>Teslim bildiriminden sonra alıcının itiraz edebileceği süre; dolunca para satıcıya geçer.</summary>
    public TimeSpan DisputeWindow { get; init; } = TimeSpan.FromHours(48);

    [Range(0, 0.5)]
    public decimal DefaultCommissionRate { get; init; } = 0.05m;

    [Range(0, 1000)]
    public decimal MinimumCommission { get; init; } = 1m;

    /// <summary>Kategori bazlı oran (örn. "fresh-fish": 0.03).</summary>
    public IReadOnlyDictionary<string, decimal> CategoryCommissionRates { get; init; } = new Dictionary<string, decimal>();

    public decimal RateFor(string categoryKey) =>
        CategoryCommissionRates.TryGetValue(categoryKey, out var rate) ? rate : DefaultCommissionRate;
}

public sealed record CheckoutSession(string Token, Uri PaymentUrl);

public sealed record VerifiedPayment(bool Succeeded, Guid OrderId, string ProviderPaymentId, decimal Amount);

/// <summary>
/// Ödeme sağlayıcısı (iyzico / PayTR pazaryeri). Para sağlayıcıda alt üye işyeri adına tutulur;
/// <see cref="ReleaseAsync"/> satıcıya aktarımı onaylar, <see cref="RefundAsync"/> alıcıya iade eder.
/// </summary>
public interface IPaymentGateway
{
    Task<CheckoutSession> CreateCheckoutAsync(PaymentOrder order, string description, CancellationToken cancellationToken);

    Task<VerifiedPayment> VerifyAsync(string token, CancellationToken cancellationToken);

    Task ReleaseAsync(PaymentOrder order, CancellationToken cancellationToken);

    Task RefundAsync(PaymentOrder order, CancellationToken cancellationToken);
}

public sealed record CommissionSummaryRow(int Year, int Month, int Orders, decimal Volume, decimal Commission);

public interface IPaymentOrderRepository
{
    Task<PaymentOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PaymentOrder?> GetOpenByOfferAsync(Guid offerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PaymentOrder>> GetForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PaymentOrder>> GetDueForMaintenanceAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken);

    Task<IReadOnlyList<PaymentOrder>> GetDisputedAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<CommissionSummaryRow>> GetCommissionSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    void Add(PaymentOrder order);
}

public sealed record PaymentOrderResponse(
    Guid Id,
    Guid ListingId,
    Guid OfferId,
    Guid BuyerId,
    Guid SellerId,
    string Role,
    decimal Amount,
    decimal Commission,
    decimal SellerPayout,
    string Currency,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset PayBy,
    DateTimeOffset? PaidAt,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? AutoReleaseAt,
    string? DisputeReason,
    string? Decision);

public sealed record CheckoutResponse(PaymentOrderResponse Order, Uri? PaymentUrl);

internal static class PaymentMapping
{
    /// <summary>Komisyon ve satıcıya geçecek tutar yalnızca satıcıya ve yöneticiye gösterilir.</summary>
    public static PaymentOrderResponse ToResponse(this PaymentOrder o, Guid viewerId, bool isAdmin)
    {
        var showSellerSide = isAdmin || viewerId == o.SellerId;
        return new PaymentOrderResponse(
            o.Id, o.ListingId, o.OfferId, o.BuyerId, o.SellerId,
            viewerId == o.BuyerId ? "Buyer" : viewerId == o.SellerId ? "Seller" : "Admin",
            o.Amount,
            showSellerSide ? o.Commission : 0,
            showSellerSide ? o.SellerPayout : 0,
            o.Currency, o.Status.ToString(), o.CreatedAt, o.PayBy, o.PaidAt, o.DeliveredAt, o.AutoReleaseAt,
            o.DisputeReason, o.Decision?.ToString());
    }
}
