using SahaNet.Domain.Common;
using SahaNet.Domain.Geo;

namespace SahaNet.Modules.Fishing.Domain;

/// <summary>
/// Kişisel av noktası. Konum şifreli saklanır ve varsayılan olarak gizlidir; yalnızca sahibinin
/// açıkça seçtiği kişilerle (konum paylaşımı rızası varsa) paylaşılır.
/// </summary>
public sealed class CatchSpot : Entity<Guid>
{
    public const int NameMaxLength = 80;
    public const int NotesMaxLength = 1000;

    private readonly List<SpotShare> _shares = [];

    private CatchSpot(Guid id, Guid ownerId, string name, GeoPoint location, DateTimeOffset now)
        : base(id)
    {
        OwnerId = ownerId;
        Name = name;
        Location = location;
        CreatedAt = now;
        UpdatedAt = now;
    }

    // EF Core
    private CatchSpot()
        : base(Guid.Empty)
    {
        Name = null!;
        Location = null!;
    }

    public Guid OwnerId { get; private set; }

    public string Name { get; private set; }

    public GeoPoint Location { get; private set; }

    public double? DepthM { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>Virgülle ayrılmış tür kodları.</summary>
    public string? SpeciesCodes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<SpotShare> Shares => _shares.AsReadOnly();

    public static CatchSpot Create(Guid ownerId, string name, GeoPoint location, double? depthM, string? notes, IEnumerable<string>? species, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(location);

        var spot = new CatchSpot(Guid.NewGuid(), ownerId, name.Trim(), location, now);
        spot.Update(name, location, depthM, notes, species, now);
        return spot;
    }

    public void Update(string name, GeoPoint location, double? depthM, string? notes, IEnumerable<string>? species, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(location);

        Name = name.Trim();
        Location = location;
        DepthM = depthM;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        var codes = species?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct(StringComparer.Ordinal).ToList();
        SpeciesCodes = codes is { Count: > 0 } ? string.Join(',', codes) : null;
        UpdatedAt = now;
    }

    public IReadOnlyList<string> SpeciesList =>
        SpeciesCodes is null ? [] : SpeciesCodes.Split(',', StringSplitOptions.RemoveEmptyEntries);

    public bool IsVisibleTo(Guid userId) => OwnerId == userId || _shares.Any(s => s.UserId == userId);

    /// <summary>Paylaşım ekler; zaten paylaşılmışsa değişiklik yapmaz.</summary>
    public Result ShareWith(Guid userId, DateTimeOffset now)
    {
        if (userId == OwnerId)
        {
            return Result.Failure(FishingErrors.CannotShareWithSelf);
        }

        if (_shares.All(s => s.UserId != userId))
        {
            _shares.Add(new SpotShare(Id, userId, now));
        }

        return Result.Success();
    }

    public void Unshare(Guid userId) => _shares.RemoveAll(s => s.UserId == userId);

    public void UnshareAll() => _shares.Clear();
}

public sealed class SpotShare
{
    internal SpotShare(Guid spotId, Guid userId, DateTimeOffset now)
    {
        SpotId = spotId;
        UserId = userId;
        SharedAt = now;
    }

    // EF Core
    private SpotShare()
    {
    }

    public Guid SpotId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset SharedAt { get; private set; }
}
