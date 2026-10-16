using SahaNet.Application.Alerts;
using SahaNet.Application.Options;
using SahaNet.Application.Weather;
using SahaNet.Domain.Alerts;
using SahaNet.Domain.Geo;

namespace SahaNet.UnitTests.Application.Alerts;

public class WeatherAlertEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 6, 0, 0, TimeSpan.Zero);
    private static readonly AlertOptions Options = new();

    private static WeatherForecast Forecast(params (int Hour, double? Wind, double? Gust, double? Wave)[] hours) => new(
        GeoPoint.FromTrusted(41, 29),
        Now,
        hours.Select(h => new WeatherHour(Now.AddHours(h.Hour), h.Wind, h.Gust, 0, h.Wave, null, null)).ToList());

    [Theory]
    [InlineData(20.0, 25.0, 0.5, AlertSeverity.Info)]
    [InlineData(40.0, 45.0, 0.5, AlertSeverity.Warning)]
    [InlineData(20.0, 55.0, 0.5, AlertSeverity.Warning)]
    [InlineData(20.0, 25.0, 2.2, AlertSeverity.Warning)]
    [InlineData(65.0, 70.0, 1.0, AlertSeverity.Critical)]
    [InlineData(30.0, 40.0, 4.0, AlertSeverity.Critical)]
    public void Classify_UsesBeaufortThresholds(double wind, double gust, double wave, AlertSeverity expected)
    {
        WeatherAlertEvaluator.Classify(new WeatherHour(Now, wind, gust, 0, wave, null, null), Options).Should().Be(expected);
    }

    [Fact]
    public void Classify_MissingWaveData_UsesWindOnly()
    {
        WeatherAlertEvaluator.Classify(new WeatherHour(Now, 70, null, 0, null, null, null), Options).Should().Be(AlertSeverity.Critical);
    }

    [Fact]
    public void Evaluate_CalmForecast_ReturnsNull()
    {
        WeatherAlertEvaluator.Evaluate(Forecast((1, 10, 15, 0.5), (2, 12, 18, 0.6)), Now, Options).Should().BeNull();
    }

    [Fact]
    public void Evaluate_PicksMostSevere_WithinLookahead()
    {
        var forecast = Forecast(
            (2, 42, 48, 1.0),
            (5, 66, 80, 3.0),
            (6, 70, 90, 3.8),
            (7, 45, 52, 2.1),
            (30, 90, 110, 5.0)); // ileriye bakış süresinin dışında

        var candidate = WeatherAlertEvaluator.Evaluate(forecast, Now, Options)!;

        candidate.Severity.Should().Be(AlertSeverity.Critical);
        candidate.From.Should().Be(Now.AddHours(5));
        candidate.To.Should().Be(Now.AddHours(7));
        candidate.PeakAt.Should().Be(Now.AddHours(6));
        candidate.PeakGustKmh.Should().Be(90);
        candidate.PeakWaveM.Should().Be(3.8);
    }

    [Fact]
    public void DedupKey_IsStablePerRegionSeverityAndDay()
    {
        var regionId = Guid.NewGuid();
        var a = new WeatherAlertCandidate(AlertSeverity.Warning, Now, Now.AddHours(3), Now, 40, 50, 2);
        var b = a with { To = Now.AddHours(9) };

        WeatherAlertEvaluator.DedupKey(regionId, a).Should().Be(WeatherAlertEvaluator.DedupKey(regionId, b));
        WeatherAlertEvaluator.DedupKey(regionId, a).Should().NotBe(WeatherAlertEvaluator.DedupKey(regionId, a with { Severity = AlertSeverity.Critical }));
    }

    [Fact]
    public void Compose_CriticalMessage_IsTurkishAndActionable()
    {
        var candidate = new WeatherAlertCandidate(AlertSeverity.Critical, Now, Now.AddHours(3), Now, 65, 82, 3.6);

        var (title, message) = WeatherAlertEvaluator.Compose("İstanbul Boğazı", candidate, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"));

        title.Should().Be("FIRTINA UYARISI — İstanbul Boğazı");
        message.Should().Contain("09:00").And.Contain("hamle 82 km/s").And.Contain("dalga 3,6 m").And.Contain("Denize çıkmayın");
    }
}
