using SahaNet.Domain.Common;

namespace SahaNet.Modules.Fishing.Domain;

/// <summary>Günlük hal fiyatı bildirimi (TL/kg). Kullanıcılar girer; günlük özet medyanla hesaplanır.</summary>
public sealed class MarketPrice : Entity<Guid>
{
    private MarketPrice(Guid id, string speciesCode, string provinceCode, DateOnly date, decimal pricePerKg, Guid reportedBy, DateTimeOffset now)
        : base(id)
    {
        SpeciesCode = speciesCode;
        ProvinceCode = provinceCode;
        Date = date;
        PricePerKg = pricePerKg;
        ReportedBy = reportedBy;
        CreatedAt = now;
    }

    // EF Core
    private MarketPrice()
        : base(Guid.Empty)
    {
        SpeciesCode = null!;
        ProvinceCode = null!;
    }

    public string SpeciesCode { get; private set; }

    /// <summary>Halin bulunduğu il (plaka kodu).</summary>
    public string ProvinceCode { get; private set; }

    public DateOnly Date { get; private set; }

    public decimal PricePerKg { get; private set; }

    public Guid ReportedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static MarketPrice Report(string speciesCode, string provinceCode, DateOnly date, decimal pricePerKg, Guid reportedBy, DateTimeOffset now) =>
        new(Guid.NewGuid(), speciesCode, provinceCode, date, pricePerKg, reportedBy, now);
}
