using SahaNet.Domain.Common;
using SahaNet.Domain.Geo;

namespace SahaNet.Modules.Fishing.Domain;

/// <summary>Balıkçı barınağı / liman. Deniz durumu liman bazlı gösterilir.</summary>
public sealed class Port : Entity<Guid>
{
    public Port(Guid id, string code, string name, string provinceCode, GeoPoint location)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(location);
        Code = code;
        Name = name;
        ProvinceCode = provinceCode;
        Location = location;
    }

    // EF Core
    private Port()
        : base(Guid.Empty)
    {
        Code = null!;
        Name = null!;
        ProvinceCode = null!;
        Location = null!;
    }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public string ProvinceCode { get; private set; }

    public GeoPoint Location { get; private set; }
}
