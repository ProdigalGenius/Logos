using Microsoft.EntityFrameworkCore;
using SahaNet.Application.Alerts;
using SahaNet.Application.Retention;
using SahaNet.Domain.Alerts;
using SahaNet.Domain.Geo;
using SahaNet.Domain.Notifications;

namespace SahaNet.Infrastructure.Persistence.Repositories;

internal sealed class RegionRepository(SahaNetDbContext db) : IRegionRepository
{
    public async Task<IReadOnlyList<Region>> GetActiveAsync(CancellationToken cancellationToken) =>
        await db.Regions.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.Code).ToListAsync(cancellationToken);

    public Task<Region?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Regions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Region>> GetContainingAsync(GeoPoint point, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(point);

        // PostGIS: geography üzerinde metre cinsinden mesafe; GiST indeksini kullanır.
        return await db.Regions
            .FromSql($"""
                SELECT * FROM alerts.regions
                WHERE is_active
                  AND ST_DWithin(center, ST_SetSRID(ST_MakePoint({point.Longitude}, {point.Latitude}), 4326)::geography, radius_km * 1000)
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Region>> GetWithSubscribersAsync(CancellationToken cancellationToken) =>
        await db.Regions.AsNoTracking()
            .Where(r => r.IsActive && db.RegionSubscriptions.Any(s => s.RegionId == r.Id))
            .ToListAsync(cancellationToken);
}

internal sealed class SubscriptionRepository(SahaNetDbContext db) : ISubscriptionRepository
{
    public Task<RegionSubscription?> GetAsync(Guid userId, Guid regionId, CancellationToken cancellationToken) =>
        db.RegionSubscriptions.FirstOrDefaultAsync(s => s.UserId == userId && s.RegionId == regionId, cancellationToken);

    public async Task<IReadOnlyList<RegionSubscription>> GetByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.RegionSubscriptions.Where(s => s.UserId == userId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RegionSubscription>> GetByRegionAsync(Guid regionId, CancellationToken cancellationToken) =>
        await db.RegionSubscriptions.AsNoTracking().Where(s => s.RegionId == regionId).ToListAsync(cancellationToken);

    public void Add(RegionSubscription subscription) => db.RegionSubscriptions.Add(subscription);

    public void Remove(RegionSubscription subscription) => db.RegionSubscriptions.Remove(subscription);
}

internal sealed class DeviceRepository(SahaNetDbContext db) : IDeviceRepository
{
    public Task<DeviceRegistration?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.Devices.FirstOrDefaultAsync(d => d.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<DeviceRegistration>> GetByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Devices.Where(d => d.UserId == userId).ToListAsync(cancellationToken);

    public void Add(DeviceRegistration device) => db.Devices.Add(device);

    public void Remove(DeviceRegistration device) => db.Devices.Remove(device);
}

internal sealed class AlertRepository(SahaNetDbContext db) : IAlertRepository
{
    public Task<Alert?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Alerts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(string dedupKey, CancellationToken cancellationToken) =>
        db.Alerts.AnyAsync(a => a.DedupKey == dedupKey, cancellationToken);

    public async Task<IReadOnlyList<Alert>> GetActiveAsync(IReadOnlyCollection<Guid>? regionIds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var query = db.Alerts.AsNoTracking().Where(a => a.ValidTo > now);
        if (regionIds is not null)
        {
            query = query.Where(a => regionIds.Contains(a.RegionId));
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>> GetDeliveredUserIdsAsync(Guid alertId, CancellationToken cancellationToken) =>
        (await db.AlertDeliveries.Where(d => d.AlertId == alertId).Select(d => d.UserId).Distinct().ToListAsync(cancellationToken))
        .ToHashSet();

    public void Add(Alert alert) => db.Alerts.Add(alert);

    public void AddDelivery(AlertDelivery delivery) => db.AlertDeliveries.Add(delivery);
}

internal sealed class DataRetentionStore(SahaNetDbContext db) : IDataRetentionStore
{
    public Task<int> DeleteOtpChallengesCreatedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken) =>
        db.OtpChallenges.Where(o => o.CreatedAt < threshold).ExecuteDeleteAsync(cancellationToken);

    public Task<int> DeleteInactiveRefreshTokensBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => (t.RevokedAt != null && t.RevokedAt < threshold) || t.ExpiresAt < threshold)
            .ExecuteDeleteAsync(cancellationToken);

    public Task<int> DeleteAlertDeliveriesBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken) =>
        db.AlertDeliveries.Where(d => d.CreatedAt < threshold).ExecuteDeleteAsync(cancellationToken);

    public Task<int> DeleteAlertsEndedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken) =>
        db.Alerts.Where(a => a.ValidTo < threshold).ExecuteDeleteAsync(cancellationToken);

    public Task<int> DeleteDevicesNotSeenSinceAsync(DateTimeOffset threshold, CancellationToken cancellationToken) =>
        db.Devices.Where(d => d.LastSeenAt < threshold).ExecuteDeleteAsync(cancellationToken);
}
