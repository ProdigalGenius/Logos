using Microsoft.EntityFrameworkCore;
using SahaNet.Application.Moderation;
using SahaNet.Domain.Alerts;
using SahaNet.Domain.Identity;
using SahaNet.Domain.Market;
using SahaNet.Domain.Moderation;
using SahaNet.Domain.Payments;

namespace SahaNet.Infrastructure.Persistence.Repositories;

internal sealed class ModerationRepository(SahaNetDbContext db) : IModerationRepository
{
    public Task<ContentReport?> GetReportAsync(Guid id, CancellationToken cancellationToken) =>
        db.Reports.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ContentReport>> GetOpenReportsForTargetAsync(ReportTargetType type, Guid targetId, CancellationToken cancellationToken) =>
        await db.Reports.Where(r => r.TargetType == type && r.TargetId == targetId && r.Status == ReportStatus.Open).ToListAsync(cancellationToken);

    public Task<bool> HasOpenReportAsync(Guid reporterId, ReportTargetType type, Guid targetId, CancellationToken cancellationToken) =>
        db.Reports.AnyAsync(r => r.ReporterId == reporterId && r.TargetType == type && r.TargetId == targetId && r.Status == ReportStatus.Open, cancellationToken);

    public Task<int> CountReportsByUserSinceAsync(Guid reporterId, DateTimeOffset since, CancellationToken cancellationToken) =>
        db.Reports.CountAsync(r => r.ReporterId == reporterId && r.CreatedAt >= since, cancellationToken);

    public async Task<(IReadOnlyList<ContentReport> Items, int Total)> GetReportsAsync(ReportStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Reports.AsNoTracking();
        if (status is { } s)
        {
            query = query.Where(r => r.Status == s);
        }

        var total = await query.CountAsync(cancellationToken);
        // Sistem şikâyetleri ve yüksek spam puanı önce değil; en eski açık şikâyet önce (FIFO, adil sıra).
        var items = await query.OrderBy(r => r.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<AuditEntry>> GetAuditAsync(int limit, CancellationToken cancellationToken) =>
        await db.AuditLog.AsNoTracking().OrderByDescending(a => a.At).Take(limit).ToListAsync(cancellationToken);

    public void Add(ContentReport report) => db.Reports.Add(report);

    public void Add(AuditEntry entry) => db.AuditLog.Add(entry);
}

/// <summary>Gösterge paneli metrikleri; tek tek COUNT/SUM sorguları (indeksli kolonlar).</summary>
internal sealed class AdminMetrics(SahaNetDbContext db) : IAdminMetrics
{
    public async Task<CoreMetrics> GetAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var d7 = now.AddDays(-7);
        var d30 = now.AddDays(-30);

        var signups = await db.Users.AsNoTracking()
            .Where(u => u.CreatedAt >= d30)
            .Select(u => u.CreatedAt)
            .ToListAsync(cancellationToken);
        var start = DateOnly.FromDateTime(d30.UtcDateTime).AddDays(1);
        var daily = Enumerable.Range(0, 30)
            .Select(i => start.AddDays(i))
            .Select(day => new DailyCount(day, signups.Count(c => DateOnly.FromDateTime(c.UtcDateTime) == day)))
            .ToList();

        var released = await db.PaymentOrders.AsNoTracking()
            .Where(o => o.Status == PaymentStatus.Released && o.ClosedAt >= d30)
            .Select(o => new { o.Amount, o.Commission })
            .ToListAsync(cancellationToken);

        return new CoreMetrics(
            TotalUsers: await db.Users.CountAsync(cancellationToken),
            NewUsers7Days: await db.Users.CountAsync(u => u.CreatedAt >= d7, cancellationToken),
            NewUsers30Days: signups.Count,
            SuspendedUsers: await db.Users.CountAsync(u => u.Status == UserStatus.Suspended, cancellationToken),
            UsersByNiche: await db.Users.AsNoTracking().Where(u => u.NicheKey != null)
                .GroupBy(u => u.NicheKey!).OrderByDescending(g => g.Count()).Select(g => new NamedCount(g.Key, g.Count())).ToListAsync(cancellationToken),
            UsersByRegion: await db.Users.AsNoTracking().Where(u => u.RegionCode != null)
                .GroupBy(u => u.RegionCode!).OrderByDescending(g => g.Count()).Take(10).Select(g => new NamedCount(g.Key, g.Count())).ToListAsync(cancellationToken),
            SignupsLast30Days: daily,
            ActiveListings: await db.Listings.CountAsync(l => l.Status == ListingStatus.Active && l.ExpiresAt > now, cancellationToken),
            ListingsLast30Days: await db.Listings.CountAsync(l => l.CreatedAt >= d30, cancellationToken),
            SalesVolume30Days: released.Sum(o => o.Amount),
            Commission30Days: released.Sum(o => o.Commission),
            OpenReports: await db.Reports.CountAsync(r => r.Status == ReportStatus.Open, cancellationToken),
            OpenDisputes: await db.PaymentOrders.CountAsync(o => o.Status == PaymentStatus.Disputed, cancellationToken),
            AlertsLast30Days: await db.Alerts.CountAsync(a => a.CreatedAt >= d30, cancellationToken),
            ActiveRegionSubscriptions: await db.RegionSubscriptions.CountAsync(cancellationToken));
    }
}
