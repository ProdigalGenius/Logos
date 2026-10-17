using SahaNet.Domain.Alerts;
using SahaNet.Domain.Geo;
using SahaNet.Domain.Notifications;

namespace SahaNet.Application.Alerts;

public interface IRegionRepository
{
    Task<IReadOnlyList<Region>> GetActiveAsync(CancellationToken cancellationToken);

    Task<Region?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Noktayı kapsayan bölgeler, merkeze yakınlığa göre (PostGIS).</summary>
    Task<IReadOnlyList<Region>> GetContainingAsync(GeoPoint point, CancellationToken cancellationToken);

    /// <summary>En az bir abonesi olan aktif bölgeler (periyodik kontrol yalnızca bunlar için).</summary>
    Task<IReadOnlyList<Region>> GetWithSubscribersAsync(CancellationToken cancellationToken);
}

public interface ISubscriptionRepository
{
    Task<RegionSubscription?> GetAsync(Guid userId, Guid regionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RegionSubscription>> GetByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RegionSubscription>> GetByRegionAsync(Guid regionId, CancellationToken cancellationToken);

    void Add(RegionSubscription subscription);

    void Remove(RegionSubscription subscription);
}

public interface IDeviceRepository
{
    Task<DeviceRegistration?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<DeviceRegistration>> GetByUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Birden çok kullanıcının cihazları tek sorguda.</summary>
    Task<IReadOnlyList<DeviceRegistration>> GetByUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    void Add(DeviceRegistration device);

    void Remove(DeviceRegistration device);
}

public interface IAlertRepository
{
    Task<Alert?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string dedupKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<Alert>> GetActiveAsync(IReadOnlyCollection<Guid>? regionIds, DateTimeOffset now, CancellationToken cancellationToken);

    Task<IReadOnlySet<Guid>> GetDeliveredUserIdsAsync(Guid alertId, CancellationToken cancellationToken);

    void Add(Alert alert);

    void AddDelivery(AlertDelivery delivery);
}

public enum PushResult
{
    Sent = 0,

    /// <summary>Token artık geçersiz (uygulama kaldırıldı); kayıt silinmeli.</summary>
    InvalidToken = 1,

    Failed = 2,
}

public sealed record PushMessage(string Title, string Body, AlertSeverity Severity, IReadOnlyDictionary<string, string> Data);

public interface IPushSender
{
    Task<PushResult> SendAsync(DeviceRegistration device, PushMessage message, CancellationToken cancellationToken);
}

/// <summary>Uyarı dağıtımını arka plana (Hangfire) atar; testlerde satır içi çalışır.</summary>
public interface IAlertDispatchScheduler
{
    Task ScheduleAsync(Guid alertId, CancellationToken cancellationToken);
}
