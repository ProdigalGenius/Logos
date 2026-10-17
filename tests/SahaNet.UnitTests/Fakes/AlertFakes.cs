using SahaNet.Application.Alerts;
using SahaNet.Application.Weather;
using SahaNet.Domain.Alerts;
using SahaNet.Domain.Geo;
using SahaNet.Domain.Notifications;

namespace SahaNet.UnitTests.Fakes;

internal sealed class InMemoryRegions : IRegionRepository
{
    public List<Region> Items { get; } = [];

    public HashSet<Guid> WithSubscribers { get; } = [];

    public Task<IReadOnlyList<Region>> GetActiveAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Region>>(Items.Where(r => r.IsActive).ToList());

    public Task<Region?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(r => r.Id == id));

    public Task<IReadOnlyList<Region>> GetContainingAsync(GeoPoint point, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Region>>(Items.Where(r => r.Contains(point)).ToList());

    public Task<IReadOnlyList<Region>> GetWithSubscribersAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Region>>(Items.Where(r => WithSubscribers.Contains(r.Id)).ToList());
}

internal sealed class InMemorySubscriptions : ISubscriptionRepository
{
    public List<RegionSubscription> Items { get; } = [];

    public Task<RegionSubscription?> GetAsync(Guid userId, Guid regionId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(s => s.UserId == userId && s.RegionId == regionId));

    public Task<IReadOnlyList<RegionSubscription>> GetByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RegionSubscription>>(Items.Where(s => s.UserId == userId).ToList());

    public Task<IReadOnlyList<RegionSubscription>> GetByRegionAsync(Guid regionId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RegionSubscription>>(Items.Where(s => s.RegionId == regionId).ToList());

    public void Add(RegionSubscription subscription) => Items.Add(subscription);

    public void Remove(RegionSubscription subscription) => Items.Remove(subscription);
}

internal sealed class InMemoryDevices : IDeviceRepository
{
    public List<DeviceRegistration> Items { get; } = [];

    public Task<DeviceRegistration?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(d => d.TokenHash == tokenHash));

    public Task<IReadOnlyList<DeviceRegistration>> GetByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DeviceRegistration>>(Items.Where(d => d.UserId == userId).ToList());

    public Task<IReadOnlyList<DeviceRegistration>> GetByUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DeviceRegistration>>(Items.Where(d => userIds.Contains(d.UserId)).ToList());

    public void Add(DeviceRegistration device) => Items.Add(device);

    public void Remove(DeviceRegistration device) => Items.Remove(device);
}

internal sealed class InMemoryAlerts : IAlertRepository
{
    public List<Alert> Items { get; } = [];

    public List<AlertDelivery> Deliveries { get; } = [];

    public Task<Alert?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(a => a.Id == id));

    public Task<bool> ExistsAsync(string dedupKey, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(a => a.DedupKey == dedupKey));

    public Task<IReadOnlyList<Alert>> GetActiveAsync(IReadOnlyCollection<Guid>? regionIds, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Alert>>(Items.Where(a => a.IsActive(now) && (regionIds is null || regionIds.Contains(a.RegionId))).ToList());

    public Task<IReadOnlySet<Guid>> GetDeliveredUserIdsAsync(Guid alertId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<Guid>>(Deliveries.Where(d => d.AlertId == alertId).Select(d => d.UserId).ToHashSet());

    public void Add(Alert alert) => Items.Add(alert);

    public void AddDelivery(AlertDelivery delivery) => Deliveries.Add(delivery);
}

internal sealed class FakePushSender : IPushSender
{
    public PushResult Result { get; set; } = PushResult.Sent;

    public List<(DeviceRegistration Device, PushMessage Message)> Sent { get; } = [];

    public Task<PushResult> SendAsync(DeviceRegistration device, PushMessage message, CancellationToken cancellationToken)
    {
        Sent.Add((device, message));
        return Task.FromResult(Result);
    }
}

internal sealed class FakeWeatherProvider(FakeClock clock) : IWeatherProvider
{
    /// <summary>Şimdiden itibaren her saat için (rüzgâr km/s, dalga m).</summary>
    public Func<int, (double Wind, double Wave)> Profile { get; set; } = _ => (10, 0.5);

    public Task<WeatherForecast> GetForecastAsync(GeoPoint location, int days, CancellationToken cancellationToken)
    {
        var start = new DateTimeOffset(clock.UtcNow.Year, clock.UtcNow.Month, clock.UtcNow.Day, clock.UtcNow.Hour, 0, 0, TimeSpan.Zero);
        var hours = Enumerable.Range(0, days * 24)
            .Select(i =>
            {
                var (wind, wave) = Profile(i);
                return new WeatherHour(start.AddHours(i), wind, wind * 1.3, 45, wave, 50, 6);
            })
            .ToList();
        return Task.FromResult(new WeatherForecast(location, clock.UtcNow, hours));
    }
}
