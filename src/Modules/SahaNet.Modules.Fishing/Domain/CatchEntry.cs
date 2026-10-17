using SahaNet.Domain.Common;

namespace SahaNet.Modules.Fishing.Domain;

/// <summary>Av günlüğü kaydı.</summary>
public sealed class CatchEntry : Entity<Guid>
{
    public const int NotesMaxLength = 1000;

    private CatchEntry(Guid id, Guid userId, DateTimeOffset caughtAt, string speciesCode, double weightKg, DateTimeOffset now)
        : base(id)
    {
        UserId = userId;
        CaughtAt = caughtAt;
        SpeciesCode = speciesCode;
        WeightKg = weightKg;
        CreatedAt = now;
    }

    // EF Core
    private CatchEntry()
        : base(Guid.Empty)
    {
        SpeciesCode = null!;
    }

    public Guid UserId { get; private set; }

    public DateTimeOffset CaughtAt { get; private set; }

    public string SpeciesCode { get; private set; }

    public double WeightKg { get; private set; }

    public int? Count { get; private set; }

    /// <summary>En büyük balığın boyu (yasal boy kontrolü için).</summary>
    public double? LengthCm { get; private set; }

    public Guid? SpotId { get; private set; }

    public Guid? PortId { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<CatchEntry> Record(
        Guid userId,
        DateTimeOffset caughtAt,
        string speciesCode,
        double weightKg,
        int? count,
        double? lengthCm,
        Guid? spotId,
        Guid? portId,
        string? notes,
        DateTimeOffset now,
        Guid? clientId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(speciesCode);
        if (caughtAt > now.AddMinutes(5))
        {
            return Result.Failure<CatchEntry>(FishingErrors.CatchInFuture);
        }

        // Çevrimdışı istemci kimliği kendisi üretir; tekrar gönderimde aynı kayıt oluşur (idempotent).
        return Result.Success(new CatchEntry(clientId is { } id && id != Guid.Empty ? id : Guid.NewGuid(), userId, caughtAt, speciesCode, weightKg, now)
        {
            Count = count,
            LengthCm = lengthCm,
            SpotId = spotId,
            PortId = portId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
        });
    }

    /// <summary>Av sezonu 1 Eylül'de başlar; "2026-2027" gibi etiketlenir.</summary>
    public static int SeasonStartYear(DateOnly date) => date.Month >= 9 ? date.Year : date.Year - 1;
}
