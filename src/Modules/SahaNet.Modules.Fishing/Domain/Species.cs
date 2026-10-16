namespace SahaNet.Modules.Fishing.Domain;

/// <summary>Balık türü kataloğu. Asgari av boyu ticari amaçlı su ürünleri tebliğine göredir.</summary>
public sealed class Species
{
    public Species(string code, string name, string? scientificName, int? minLengthCm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Code = code;
        Name = name;
        ScientificName = scientificName;
        MinLengthCm = minLengthCm;
    }

    // EF Core
    private Species()
    {
        Code = null!;
        Name = null!;
    }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public string? ScientificName { get; private set; }

    /// <summary>Asgari yasal av boyu (cm); sınır yoksa null.</summary>
    public int? MinLengthCm { get; private set; }

    public bool IsUndersized(double lengthCm) => MinLengthCm is { } min && lengthCm < min;
}
