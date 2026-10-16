using SahaNet.Domain.Common;

namespace SahaNet.Modules.Fishing.Domain;

/// <summary>Her yıl tekrarlayan av yasağı dönemi. Tür, bölge ve av aracı boş ise tümü kapsanır.</summary>
public sealed class FishingBan : Entity<Guid>
{
    public FishingBan(
        Guid id,
        string title,
        int startMonth,
        int startDay,
        int endMonth,
        int endDay,
        string? speciesCode,
        string? regionCode,
        string? gear,
        string? notes)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        _ = new DateOnly(2024, startMonth, startDay); // geçerlilik (artık yıl dahil)
        _ = new DateOnly(2024, endMonth, endDay);

        Title = title;
        StartMonth = startMonth;
        StartDay = startDay;
        EndMonth = endMonth;
        EndDay = endDay;
        SpeciesCode = speciesCode;
        RegionCode = regionCode;
        Gear = gear;
        Notes = notes;
    }

    // EF Core
    private FishingBan()
        : base(Guid.Empty)
    {
        Title = null!;
    }

    public string Title { get; private set; }

    public int StartMonth { get; private set; }

    public int StartDay { get; private set; }

    public int EndMonth { get; private set; }

    public int EndDay { get; private set; }

    /// <summary>Null ise tüm türler.</summary>
    public string? SpeciesCode { get; private set; }

    /// <summary>Çekirdek bölge kodu (örn. "karadeniz-bati"); null ise tüm denizler.</summary>
    public string? RegionCode { get; private set; }

    /// <summary>Av aracı (gırgır, trol ...); null ise tümü.</summary>
    public string? Gear { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>Tarih yasak dönemi içinde mi (yıl sonunu aşan dönemler dahil).</summary>
    public bool IsActiveOn(DateOnly date)
    {
        var key = (date.Month * 100) + date.Day;
        var start = (StartMonth * 100) + StartDay;
        var end = (EndMonth * 100) + EndDay;
        return start <= end ? key >= start && key <= end : key >= start || key <= end;
    }

    public bool Applies(string? speciesCode, string? regionCode) =>
        (SpeciesCode is null || speciesCode is null || string.Equals(SpeciesCode, speciesCode, StringComparison.Ordinal)) &&
        (RegionCode is null || regionCode is null || string.Equals(RegionCode, regionCode, StringComparison.Ordinal));
}
