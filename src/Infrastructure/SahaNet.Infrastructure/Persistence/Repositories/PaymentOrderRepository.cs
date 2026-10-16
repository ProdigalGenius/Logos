using Microsoft.EntityFrameworkCore;
using SahaNet.Application.Payments;
using SahaNet.Domain.Payments;

namespace SahaNet.Infrastructure.Persistence.Repositories;

internal sealed class PaymentOrderRepository(SahaNetDbContext db) : IPaymentOrderRepository
{
    public Task<PaymentOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.PaymentOrders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<PaymentOrder?> GetOpenByOfferAsync(Guid offerId, CancellationToken cancellationToken) =>
        db.PaymentOrders.FirstOrDefaultAsync(o => o.OfferId == offerId && o.Status != PaymentStatus.Cancelled, cancellationToken);

    public async Task<IReadOnlyList<PaymentOrder>> GetForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.PaymentOrders.AsNoTracking()
            .Where(o => o.BuyerId == userId || o.SellerId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PaymentOrder>> GetDueForMaintenanceAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken) =>
        await db.PaymentOrders
            .Where(o => (o.Status == PaymentStatus.AwaitingPayment && o.PayBy <= now) ||
                        (o.Status == PaymentStatus.Delivered && o.AutoReleaseAt <= now))
            .OrderBy(o => o.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PaymentOrder>> GetDisputedAsync(CancellationToken cancellationToken) =>
        await db.PaymentOrders.AsNoTracking()
            .Where(o => o.Status == PaymentStatus.Disputed)
            .OrderBy(o => o.DisputedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CommissionSummaryRow>> GetCommissionSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var released = await db.PaymentOrders.AsNoTracking()
            .Where(o => o.Status == PaymentStatus.Released && o.ClosedAt >= from && o.ClosedAt < to)
            .Select(o => new { o.ClosedAt, o.Amount, o.Commission })
            .ToListAsync(cancellationToken);

        return released
            .GroupBy(o => (o.ClosedAt!.Value.Year, o.ClosedAt!.Value.Month))
            .Select(g => new CommissionSummaryRow(g.Key.Year, g.Key.Month, g.Count(), g.Sum(o => o.Amount), g.Sum(o => o.Commission)))
            .OrderBy(r => r.Year).ThenBy(r => r.Month)
            .ToList();
    }

    public void Add(PaymentOrder order) => db.PaymentOrders.Add(order);
}
