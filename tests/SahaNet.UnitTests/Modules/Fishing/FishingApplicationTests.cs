using SahaNet.Application.Options;
using SahaNet.Application.Weather;
using SahaNet.Modules.Fishing.Application;
using SahaNet.Modules.Fishing.Domain;

namespace SahaNet.UnitTests.Modules.Fishing;

public class FishingApplicationTests
{
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
    private static readonly DateTimeOffset Start = new(2026, 10, 16, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0.5, 0)]
    [InlineData(10, 2)]
    [InlineData(38.9, 5)]
    [InlineData(39, 6)]
    [InlineData(62, 8)]
    [InlineData(130, 12)]
    public void Beaufort_Scale(double kmh, int expected)
    {
        SeaStatusCalculator.Beaufort(kmh).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, "Yıldız")]
    [InlineData(45, "Poyraz")]
    [InlineData(225, "Lodos")]
    [InlineData(315, "Karayel")]
    [InlineData(350, "Yıldız")]
    [InlineData(-45, "Karayel")]
    public void WindNames_AreTurkish(double degrees, string expected)
    {
        SeaStatusCalculator.WindFrom(degrees).Name.Should().Be(expected);
    }

    [Fact]
    public void DominantDirection_HandlesNorthWrapAround()
    {
        var hours = new[]
        {
            new WeatherHour(Start, 20, null, 350, null, null, null),
            new WeatherHour(Start, 20, null, 10, null, null, null),
        };

        var direction = SeaStatusCalculator.DominantDirection(hours)!.Value;

        (direction < 1 || direction > 359).Should().BeTrue();
    }

    [Fact]
    public void Days_GroupByLocalDate_AndTakeWorstVerdict()
    {
        // 21:00 UTC = ertesi gün 00:00 İstanbul.
        var hours = new[]
        {
            new WeatherHour(Start.AddHours(6), 15, 20, 225, 0.5, null, null),
            new WeatherHour(Start.AddHours(12), 45, 55, 225, 1.5, null, null),
            new WeatherHour(Start.AddHours(21), 70, 90, 45, 4.0, null, null),
        };

        var days = SeaStatusCalculator.Days(hours, new AlertOptions(), Istanbul);

        days.Should().HaveCount(2);
        days[0].Verdict.Should().Be(SeaVerdict.Caution);
        days[0].DominantWindName.Should().Be("Lodos");
        days[0].Summary.Should().Contain("dikkatli");
        days[1].Date.Should().Be(new DateOnly(2026, 10, 17));
        days[1].Verdict.Should().Be(SeaVerdict.NoGo);
        days[1].Summary.Should().Contain("Poyraz").And.Contain("çıkmayın");
    }

    [Fact]
    public void CatchWarnings_UndersizedAndSpeciesBan()
    {
        var kalkan = new Species("kalkan", "Kalkan", null, 45);
        var bans = new[]
        {
            new FishingBan(Guid.NewGuid(), "Kalkan av yasağı", 4, 15, 6, 15, "kalkan", null, null, null),
            new FishingBan(Guid.NewGuid(), "Genel", 4, 15, 8, 31, null, null, "Trol", null),
        };

        var warnings = CatchWarnings.For(kalkan, 40, new DateOnly(2026, 5, 1), bans);

        warnings.Should().HaveCount(2);
        warnings[0].Should().Contain("45 cm").And.Contain("40 cm");
        warnings[1].Should().Contain("Kalkan av yasağı");
        CatchWarnings.For(kalkan, 50, new DateOnly(2026, 10, 1), bans).Should().BeEmpty();
    }

    [Fact]
    public void SeasonStats_AggregatesBySpeciesMonthAndDay()
    {
        var user = Guid.NewGuid();
        var now = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);
        CatchEntry Entry(DateTimeOffset at, string species, double kg) =>
            CatchEntry.Record(user, at, species, kg, null, null, null, null, null, now).Value;

        var catches = new[]
        {
            Entry(new DateTimeOffset(2026, 10, 1, 5, 0, 0, TimeSpan.Zero), "levrek", 3.5),
            Entry(new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero), "lufer", 1.25),
            Entry(new DateTimeOffset(2026, 11, 2, 5, 0, 0, TimeSpan.Zero), "levrek", 2),
        };
        var names = new Dictionary<string, string> { ["levrek"] = "Levrek", ["lufer"] = "Lüfer" };

        var stats = SeasonStats.Build(2026, new DateOnly(2026, 9, 1), new DateOnly(2027, 8, 31), catches, names, Istanbul);

        stats.Season.Should().Be("2026-2027");
        stats.TotalWeightKg.Should().Be(6.75);
        stats.FishingDays.Should().Be(2);
        stats.BySpecies[0].Should().Be(new SpeciesTotal("levrek", "Levrek", 5.5, 2));
        stats.ByMonth.Should().HaveCount(2);
        stats.ByMonth[0].Trips.Should().Be(1);
    }
}
