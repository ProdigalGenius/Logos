using SahaNet.Domain.Common;

namespace SahaNet.Modules.Scrap.Domain;

public static class ScrapErrors
{
    public static readonly Error MaterialUnknown = new("scrap.material_unknown", "Malzeme bulunamadı.", ErrorType.Validation);
    public static readonly Error PickupNotFound = new("scrap.pickup_not_found", "Toplama talebi bulunamadı.", ErrorType.NotFound);
    public static readonly Error PickupNotOpen = new("scrap.pickup_not_open", "Talep başka bir hurdacı tarafından alındı veya kapandı.", ErrorType.Conflict);
    public static readonly Error OwnPickup = new("scrap.own_pickup", "Kendi talebinizi üstlenemezsiniz.", ErrorType.Validation);
    public static readonly Error NotCollector = new("scrap.not_collector", "Bu işlem talebi üstlenen hurdacıya açık.", ErrorType.Forbidden);
    public static readonly Error NotRequester = new("scrap.not_requester", "Bu işlem talep sahibine açık.", ErrorType.Forbidden);
    public static readonly Error OpenLimit = new("scrap.open_limit", "Aynı anda en fazla 5 açık talebiniz olabilir.", ErrorType.Conflict);
}

/// <summary>Hurda malzemesi (fiyatlar TL/kg).</summary>
public sealed class Material
{
    public Material(string code, string name, string group)
    {
        Code = code;
        Name = name;
        Group = group;
    }

    // EF Core
    private Material()
    {
        Code = null!;
        Name = null!;
        Group = null!;
    }

    public string Code { get; private set; }

    public string Name { get; private set; }

    /// <summary>Metal, kağıt, plastik, elektronik ...</summary>
    public string Group { get; private set; }
}

/// <summary>Günlük hurda fiyatı bildirimi (kişi başı gün/malzeme/il için tek kayıt).</summary>
public sealed class ScrapPriceReport : Entity<Guid>
{
    private ScrapPriceReport(Guid id)
        : base(id)
    {
        MaterialCode = null!;
        ProvinceCode = null!;
    }

    // EF Core
    private ScrapPriceReport()
        : this(Guid.Empty)
    {
    }

    public string MaterialCode { get; private set; }

    public string ProvinceCode { get; private set; }

    public DateOnly Date { get; private set; }

    public decimal PricePerKg { get; private set; }

    public Guid ReportedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static ScrapPriceReport Create(string materialCode, string provinceCode, DateOnly date, decimal pricePerKg, Guid reportedBy, DateTimeOffset now) =>
        new(Guid.NewGuid())
        {
            MaterialCode = materialCode,
            ProvinceCode = provinceCode,
            Date = date,
            PricePerKg = pricePerKg,
            ReportedBy = reportedBy,
            CreatedAt = now,
        };
}

public enum PickupStatus
{
    Open = 0,
    Claimed = 1,
    Completed = 2,
    Cancelled = 3,
}

/// <summary>
/// "Evimde hurda var, gelip alın" talebi. Açık adres şifreli saklanır ve yalnızca talep sahibine ve talebi
/// üstlenen hurdacıya gösterilir; diğer hurdacılar yalnızca il/ilçe ve malzemeyi görür (KVKK: en az veri).
/// </summary>
public sealed class PickupRequest : Entity<Guid>
{
    public const int AddressMaxLength = 500;
    public const int NotesMaxLength = 500;

    private PickupRequest(Guid id)
        : base(id)
    {
        ProvinceCode = null!;
        District = null!;
        Address = null!;
        MaterialCodes = null!;
    }

    // EF Core
    private PickupRequest()
        : this(Guid.Empty)
    {
    }

    public Guid RequesterId { get; private set; }

    public string ProvinceCode { get; private set; }

    public string District { get; private set; }

    /// <summary>Şifreli kolonda saklanır.</summary>
    public string Address { get; private set; }

    /// <summary>Virgülle ayrılmış malzeme kodları.</summary>
    public string MaterialCodes { get; private set; }

    public decimal EstimatedKg { get; private set; }

    public string? Notes { get; private set; }

    public PickupStatus Status { get; private set; }

    public Guid? CollectorId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ClaimedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public IReadOnlyList<string> Materials => MaterialCodes.Split(',', StringSplitOptions.RemoveEmptyEntries);

    public static PickupRequest Create(
        Guid requesterId, string provinceCode, string district, string address, IEnumerable<string> materials, decimal estimatedKg, string? notes, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(estimatedKg);
        var codes = materials.Distinct(StringComparer.Ordinal).ToList();
        if (codes.Count == 0)
        {
            throw new ArgumentException("En az bir malzeme gerekli.", nameof(materials));
        }

        return new PickupRequest(Guid.NewGuid())
        {
            RequesterId = requesterId,
            ProvinceCode = provinceCode,
            District = district.Trim(),
            Address = address.Trim(),
            MaterialCodes = string.Join(',', codes),
            EstimatedKg = estimatedKg,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Status = PickupStatus.Open,
            CreatedAt = now,
        };
    }

    public bool CanSeeAddress(Guid userId) => userId == RequesterId || (CollectorId == userId && Status is PickupStatus.Claimed or PickupStatus.Completed);

    public Result Claim(Guid collectorId, DateTimeOffset now)
    {
        if (collectorId == RequesterId)
        {
            return Result.Failure(ScrapErrors.OwnPickup);
        }

        if (Status != PickupStatus.Open)
        {
            return Result.Failure(ScrapErrors.PickupNotOpen);
        }

        Status = PickupStatus.Claimed;
        CollectorId = collectorId;
        ClaimedAt = now;
        return Result.Success();
    }

    /// <summary>Hurdacı vazgeçerse talep tekrar açılır.</summary>
    public Result Release(Guid collectorId)
    {
        if (Status != PickupStatus.Claimed || CollectorId != collectorId)
        {
            return Result.Failure(ScrapErrors.NotCollector);
        }

        Status = PickupStatus.Open;
        CollectorId = null;
        ClaimedAt = null;
        return Result.Success();
    }

    public Result Complete(Guid collectorId, DateTimeOffset now)
    {
        if (Status != PickupStatus.Claimed || CollectorId != collectorId)
        {
            return Result.Failure(ScrapErrors.NotCollector);
        }

        Status = PickupStatus.Completed;
        ClosedAt = now;
        return Result.Success();
    }

    public Result Cancel(Guid requesterId, DateTimeOffset now)
    {
        if (requesterId != RequesterId)
        {
            return Result.Failure(ScrapErrors.NotRequester);
        }

        if (Status is PickupStatus.Completed or PickupStatus.Cancelled)
        {
            return Result.Failure(ScrapErrors.PickupNotOpen);
        }

        Status = PickupStatus.Cancelled;
        ClosedAt = now;
        return Result.Success();
    }
}
