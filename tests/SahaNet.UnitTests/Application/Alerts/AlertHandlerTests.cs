using Microsoft.Extensions.Logging.Abstractions;
using SahaNet.Application.Alerts;
using SahaNet.Application.Options;
using SahaNet.Domain.Alerts;
using SahaNet.Domain.Geo;
using SahaNet.Domain.Identity;
using SahaNet.Domain.Notifications;
using SahaNet.UnitTests.Fakes;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace SahaNet.UnitTests.Application.Alerts;

public class AlertHandlerTests
{
    private readonly IdentityHarness _h = new();
    private readonly InMemoryRegions _regions = new();
    private readonly InMemorySubscriptions _subscriptions = new();
    private readonly InMemoryDevices _devices = new();
    private readonly InMemoryAlerts _alerts = new();
    private readonly FakePushSender _push = new();
    private readonly AlertOptions _alertOptions = new();
    private readonly FakeWeatherProvider _weather;
    private readonly Region _region = new(Guid.NewGuid(), "marmara", "Marmara", RegionKind.SeaArea, GeoPoint.FromTrusted(40.7, 28.5), 50, null);

    public AlertHandlerTests()
    {
        _weather = new FakeWeatherProvider(_h.Clock);
        _regions.Items.Add(_region);
        _regions.WithSubscribers.Add(_region.Id);
    }

    private CheckWeatherAlertsCommandHandler CheckHandler() => new(
        _regions, _alerts, _weather, _h.UnitOfWork, _h.Clock, MsOptions.Create(_alertOptions), NullLogger<CheckWeatherAlertsCommandHandler>.Instance);

    private DispatchAlertCommandHandler DispatchHandler() => new(
        _alerts, _regions, _subscriptions, _devices, _h.Users, _push, _h.Sms, _h.UnitOfWork, _h.Clock,
        MsOptions.Create(_alertOptions), NullLogger<DispatchAlertCommandHandler>.Instance);

    private User Subscriber(string phone, AlertTopics topics, bool withDevice)
    {
        var user = User.Register(PhoneNumber.Create(phone).Value, "h:" + phone, _h.Clock.UtcNow);
        _h.Users.Add(user);
        _subscriptions.Add(RegionSubscription.Create(user.Id, _region.Id, topics, _h.Clock.UtcNow));
        if (withDevice)
        {
            _devices.Add(DeviceRegistration.Register(user.Id, "tok-" + phone, "dh:" + phone, DevicePlatform.Android, _h.Clock.UtcNow));
        }

        return user;
    }

    private Alert IssueAlert(AlertSeverity severity)
    {
        var alert = Alert.Issue(_region.Id, AlertKind.Weather, severity, "Başlık", "Mesaj", _h.Clock.UtcNow, _h.Clock.UtcNow.AddHours(6), Guid.NewGuid().ToString(), _h.Clock.UtcNow);
        _alerts.Add(alert);
        return alert;
    }

    [Fact]
    public async Task Check_StormForecast_IssuesSingleCriticalAlert()
    {
        _weather.Profile = i => i is >= 3 and <= 6 ? (70, 4.0) : (15, 0.5);

        var first = await CheckHandler().Handle(new CheckWeatherAlertsCommand(), CancellationToken.None);
        var second = await CheckHandler().Handle(new CheckWeatherAlertsCommand(), CancellationToken.None);

        first.AlertsIssued.Should().Be(1);
        second.AlertsIssued.Should().Be(0, "aynı olay için tekrar uyarı üretilmez");
        _alerts.Items.Should().ContainSingle().Which.Severity.Should().Be(AlertSeverity.Critical);
        _alerts.Items[0].DomainEvents.OfType<AlertIssued>().Should().ContainSingle();
    }

    [Fact]
    public async Task Check_CalmForecast_IssuesNothing()
    {
        var result = await CheckHandler().Handle(new CheckWeatherAlertsCommand(), CancellationToken.None);

        result.RegionsChecked.Should().Be(1);
        _alerts.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Dispatch_Warning_OnlyToWeatherSubscribers()
    {
        Subscriber("05320000001", AlertTopics.Weather, withDevice: true);
        Subscriber("05320000002", AlertTopics.Market, withDevice: true);
        var alert = IssueAlert(AlertSeverity.Warning);

        var result = await DispatchHandler().Handle(new DispatchAlertCommand(alert.Id), CancellationToken.None);

        result.PushSent.Should().Be(1);
        result.Skipped.Should().Be(1);
        _push.Sent.Should().ContainSingle().Which.Message.Severity.Should().Be(AlertSeverity.Warning);
        _h.Sms.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Dispatch_Critical_ReachesEveryone_WithSmsFallback()
    {
        Subscriber("05320000001", AlertTopics.Market, withDevice: true);
        var noDevice = Subscriber("05320000002", AlertTopics.Weather, withDevice: false);
        var alert = IssueAlert(AlertSeverity.Critical);

        var result = await DispatchHandler().Handle(new DispatchAlertCommand(alert.Id), CancellationToken.None);

        result.Recipients.Should().Be(2);
        result.PushSent.Should().Be(1);
        result.SmsSent.Should().Be(1);
        _h.Sms.Sent.Should().ContainSingle().Which.To.Should().Be(noDevice.Phone);
        _alerts.Deliveries.Should().HaveCount(2);
    }

    [Fact]
    public async Task Dispatch_IsIdempotent()
    {
        Subscriber("05320000001", AlertTopics.Weather, withDevice: true);
        var alert = IssueAlert(AlertSeverity.Critical);

        await DispatchHandler().Handle(new DispatchAlertCommand(alert.Id), CancellationToken.None);
        var second = await DispatchHandler().Handle(new DispatchAlertCommand(alert.Id), CancellationToken.None);

        second.Recipients.Should().Be(0);
        _push.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task Dispatch_InvalidToken_RemovesDevice_AndFallsBackToSms()
    {
        Subscriber("05320000001", AlertTopics.Weather, withDevice: true);
        _push.Result = PushResult.InvalidToken;
        var alert = IssueAlert(AlertSeverity.Critical);

        var result = await DispatchHandler().Handle(new DispatchAlertCommand(alert.Id), CancellationToken.None);

        _devices.Items.Should().BeEmpty();
        result.SmsSent.Should().Be(1);
    }

    [Fact]
    public async Task Dispatch_SkipsUsersPendingDeletion_AndExpiredAlerts()
    {
        var user = Subscriber("05320000001", AlertTopics.Weather, withDevice: true);
        user.RequestDeletion(_h.Clock.UtcNow, TimeSpan.FromDays(30));
        var alert = IssueAlert(AlertSeverity.Critical);

        (await DispatchHandler().Handle(new DispatchAlertCommand(alert.Id), CancellationToken.None)).Recipients.Should().Be(0);

        _h.Clock.Advance(TimeSpan.FromHours(7));
        user.CancelDeletion();
        (await DispatchHandler().Handle(new DispatchAlertCommand(alert.Id), CancellationToken.None)).Recipients.Should().Be(0);
    }

    [Fact]
    public async Task Subscribe_IsIdempotent_AndUpdatesTopics()
    {
        _regions.Items.Add(new Region(Guid.NewGuid(), "ege", "Ege", RegionKind.SeaArea, GeoPoint.FromTrusted(38.5, 26.4), 90, null));
        var user = _h.AddUser();
        var handler = new SubscribeToRegionCommandHandler(_h.CurrentUser, _subscriptions, _regions, _h.UnitOfWork, _h.Clock);

        await handler.Handle(new SubscribeToRegionCommand(_region.Id, null), CancellationToken.None);
        var result = await handler.Handle(new SubscribeToRegionCommand(_region.Id, ["weather", "Market"]), CancellationToken.None);

        _subscriptions.Items.Should().ContainSingle(s => s.UserId == user.Id);
        result.Value.Should().ContainSingle().Which.Topics.Should().BeEquivalentTo("Weather", "Market");
    }

    [Fact]
    public async Task Subscribe_UnknownRegion_Fails()
    {
        _h.AddUser();
        var handler = new SubscribeToRegionCommandHandler(_h.CurrentUser, _subscriptions, _regions, _h.UnitOfWork, _h.Clock);

        var result = await handler.Handle(new SubscribeToRegionCommand(Guid.NewGuid(), null), CancellationToken.None);

        result.Error.Should().Be(AlertErrors.RegionNotFound);
    }

    [Fact]
    public async Task RegisterDevice_SameToken_MovesToNewOwner()
    {
        var first = _h.AddUser();
        var handler = new RegisterDeviceCommandHandler(_h.CurrentUser, _devices, _h.Protector, _h.UnitOfWork, _h.Clock);
        await handler.Handle(new RegisterDeviceCommand("fcm-token", "android"), CancellationToken.None);

        var second = User.Register(PhoneNumber.Create("05550000000").Value, "h:other", _h.Clock.UtcNow);
        _h.Users.Add(second);
        _h.CurrentUser.UserId = second.Id;
        await handler.Handle(new RegisterDeviceCommand("fcm-token", "Ios"), CancellationToken.None);

        _devices.Items.Should().ContainSingle().Which.UserId.Should().Be(second.Id);
        _devices.Items[0].Platform.Should().Be(DevicePlatform.Ios);
        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public async Task Nearby_ReturnsContainingRegions_SortedByDistance()
    {
        var handler = new GetNearbyRegionsQueryHandler(_regions);

        var inside = await handler.Handle(new GetNearbyRegionsQuery(40.75, 28.6), CancellationToken.None);
        var invalid = await handler.Handle(new GetNearbyRegionsQuery(95, 28.6), CancellationToken.None);

        inside.Value.Should().ContainSingle().Which.Region.Code.Should().Be("marmara");
        invalid.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Forecast_IncludesActiveAlerts()
    {
        IssueAlert(AlertSeverity.Warning);
        var handler = new GetRegionForecastQueryHandler(_regions, _alerts, _weather, _h.Clock);

        var result = await handler.Handle(new GetRegionForecastQuery(_region.Id), CancellationToken.None);

        result.Value.Hours.Should().HaveCount(72);
        result.Value.ActiveAlerts.Should().ContainSingle();
    }
}
