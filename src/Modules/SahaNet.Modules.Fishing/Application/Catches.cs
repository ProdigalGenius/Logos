using System.Globalization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Application.Options;
using SahaNet.Domain.Common;
using SahaNet.Domain.Identity;
using SahaNet.Modules.Fishing.Domain;
using SahaNet.Modules.Fishing.Persistence;

namespace SahaNet.Modules.Fishing.Application;

public sealed record CatchResponse(
    Guid Id,
    DateTimeOffset CaughtAt,
    string SpeciesCode,
    string SpeciesName,
    double WeightKg,
    int? Count,
    double? LengthCm,
    Guid? SpotId,
    Guid? PortId,
    string? Notes);

/// <summary>Kayıt engellenmez; balıkçı yasal boy ve yasak dönemi konusunda uyarılır.</summary>
public sealed record RecordCatchResponse(CatchResponse Catch, IReadOnlyList<string> Warnings);

public sealed record SpeciesTotal(string SpeciesCode, string SpeciesName, double WeightKg, int Entries);

public sealed record MonthTotal(int Year, int Month, double WeightKg, int Trips);

public sealed record SeasonStatsResponse(
    string Season,
    DateOnly From,
    DateOnly To,
    double TotalWeightKg,
    int Entries,
    int FishingDays,
    IReadOnlyList<SpeciesTotal> BySpecies,
    IReadOnlyList<MonthTotal> ByMonth);

// ---- Av kaydı ekle ----

public sealed record RecordCatchCommand(
    DateTimeOffset CaughtAt,
    string SpeciesCode,
    double WeightKg,
    int? Count,
    double? LengthCm,
    Guid? SpotId,
    Guid? PortId,
    string? Notes,
    Guid? ClientId = null) : IRequest<Result<RecordCatchResponse>>;

internal sealed class RecordCatchCommandValidator : AbstractValidator<RecordCatchCommand>
{
    public RecordCatchCommandValidator()
    {
        RuleFor(x => x.SpeciesCode).NotEmpty().MaximumLength(32);
        RuleFor(x => x.WeightKg).GreaterThan(0).LessThanOrEqualTo(50_000);
        RuleFor(x => x.Count).GreaterThan(0).When(x => x.Count is not null);
        RuleFor(x => x.LengthCm).InclusiveBetween(0.5, 600).When(x => x.LengthCm is not null);
        RuleFor(x => x.Notes).MaximumLength(CatchEntry.NotesMaxLength);
    }
}

internal sealed class RecordCatchCommandHandler(
    ICurrentUser currentUser,
    FishingDbContext db,
    IDateTimeProvider clock,
    IOptions<AlertOptions> alertOptions)
    : IRequestHandler<RecordCatchCommand, Result<RecordCatchResponse>>
{
    public async Task<Result<RecordCatchResponse>> Handle(RecordCatchCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<RecordCatchResponse>(IdentityErrors.Unauthenticated);
        }

        var species = await db.Species.AsNoTracking().FirstOrDefaultAsync(s => s.Code == request.SpeciesCode, cancellationToken);
        if (species is null)
        {
            return Result.Failure<RecordCatchResponse>(FishingErrors.SpeciesUnknown);
        }

        // Çevrimdışı senkronizasyon: aynı istemci kimliğiyle tekrar gelen kayıt mevcut olanı döndürür.
        if (request.ClientId is { } clientId &&
            await db.Catches.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken) is { } existing)
        {
            return existing.UserId == userId
                ? Result.Success(new RecordCatchResponse(existing.ToResponse(species.Name), []))
                : Result.Failure<RecordCatchResponse>(FishingErrors.CatchNotFound);
        }

        if (request.SpotId is { } spotId &&
            !await db.Spots.AnyAsync(s => s.Id == spotId && (s.OwnerId == userId || s.Shares.Any(x => x.UserId == userId)), cancellationToken))
        {
            return Result.Failure<RecordCatchResponse>(FishingErrors.SpotNotFound);
        }

        if (request.PortId is { } portId && !await db.Ports.AnyAsync(p => p.Id == portId, cancellationToken))
        {
            return Result.Failure<RecordCatchResponse>(FishingErrors.PortNotFound);
        }

        var entry = CatchEntry.Record(
            userId, request.CaughtAt.ToUniversalTime(), species.Code, request.WeightKg, request.Count, request.LengthCm,
            request.SpotId, request.PortId, request.Notes, clock.UtcNow, request.ClientId);
        if (entry.IsFailure)
        {
            return Result.Failure<RecordCatchResponse>(entry.Error);
        }

        db.Catches.Add(entry.Value);
        await db.SaveChangesAsync(cancellationToken);

        var zone = TimeZoneInfo.FindSystemTimeZoneById(alertOptions.Value.DisplayTimeZone);
        var catchDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(entry.Value.CaughtAt, zone).DateTime);
        var bans = await db.Bans.AsNoTracking().ToListAsync(cancellationToken);
        var warnings = CatchWarnings.For(species, request.LengthCm, catchDay, bans);

        return Result.Success(new RecordCatchResponse(entry.Value.ToResponse(species.Name), warnings));
    }
}

internal static class CatchWarnings
{
    public static IReadOnlyList<string> For(Species species, double? lengthCm, DateOnly day, IEnumerable<FishingBan> bans)
    {
        var warnings = new List<string>();
        if (lengthCm is { } length && species.IsUndersized(length))
        {
            warnings.Add(string.Create(CultureInfo.GetCultureInfo("tr-TR"),
                $"{species.Name} için asgari av boyu {species.MinLengthCm} cm; girilen {length:0.#} cm. Küçük balığı denize iade edin."));
        }

        // Yalnızca türe özel yasaklar uyarılır; genel (av aracı) yasakları amatör avı kapsamayabilir.
        warnings.AddRange(bans
            .Where(b => b.SpeciesCode == species.Code && b.IsActiveOn(day))
            .Select(b => $"Av yasağı dönemi: {b.Title}."));
        return warnings;
    }
}

internal static class CatchMapping
{
    public static CatchResponse ToResponse(this CatchEntry c, string speciesName) =>
        new(c.Id, c.CaughtAt, c.SpeciesCode, speciesName, c.WeightKg, c.Count, c.LengthCm, c.SpotId, c.PortId, c.Notes);
}

// ---- Av günlüğüm ----

public sealed record GetMyCatchesQuery(DateTimeOffset? From, DateTimeOffset? To, int Limit = 100) : IRequest<Result<IReadOnlyList<CatchResponse>>>;

internal sealed class GetMyCatchesQueryValidator : AbstractValidator<GetMyCatchesQuery>
{
    public GetMyCatchesQueryValidator() => RuleFor(x => x.Limit).InclusiveBetween(1, 500);
}

internal sealed class GetMyCatchesQueryHandler(ICurrentUser currentUser, FishingDbContext db)
    : IRequestHandler<GetMyCatchesQuery, Result<IReadOnlyList<CatchResponse>>>
{
    public async Task<Result<IReadOnlyList<CatchResponse>>> Handle(GetMyCatchesQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<IReadOnlyList<CatchResponse>>(IdentityErrors.Unauthenticated);
        }

        var query = db.Catches.AsNoTracking().Where(c => c.UserId == userId);
        if (request.From is { } from)
        {
            query = query.Where(c => c.CaughtAt >= from.ToUniversalTime());
        }

        if (request.To is { } to)
        {
            query = query.Where(c => c.CaughtAt < to.ToUniversalTime());
        }

        var catches = await query.OrderByDescending(c => c.CaughtAt).Take(request.Limit).ToListAsync(cancellationToken);
        var names = await db.Species.AsNoTracking().ToDictionaryAsync(s => s.Code, s => s.Name, cancellationToken);
        return Result.Success<IReadOnlyList<CatchResponse>>(
            catches.Select(c => c.ToResponse(names.GetValueOrDefault(c.SpeciesCode, c.SpeciesCode))).ToList());
    }
}

public sealed record DeleteCatchCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteCatchCommandHandler(ICurrentUser currentUser, FishingDbContext db) : IRequestHandler<DeleteCatchCommand, Result>
{
    public async Task<Result> Handle(DeleteCatchCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(IdentityErrors.Unauthenticated);
        }

        var entry = await db.Catches.FirstOrDefaultAsync(c => c.Id == request.Id && c.UserId == userId, cancellationToken);
        if (entry is null)
        {
            return Result.Failure(FishingErrors.CatchNotFound);
        }

        db.Catches.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

// ---- Sezon istatistiği ----

/// <summary>Sezon başlangıç yılı verilmezse içinde bulunulan sezon (1 Eylül – 31 Ağustos).</summary>
public sealed record GetSeasonStatsQuery(int? SeasonStartYear) : IRequest<Result<SeasonStatsResponse>>;

internal sealed class GetSeasonStatsQueryHandler(
    ICurrentUser currentUser,
    FishingDbContext db,
    IDateTimeProvider clock,
    IOptions<AlertOptions> alertOptions)
    : IRequestHandler<GetSeasonStatsQuery, Result<SeasonStatsResponse>>
{
    public async Task<Result<SeasonStatsResponse>> Handle(GetSeasonStatsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<SeasonStatsResponse>(IdentityErrors.Unauthenticated);
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(alertOptions.Value.DisplayTimeZone);
        var year = request.SeasonStartYear ?? CatchEntry.SeasonStartYear(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, zone).DateTime));
        var from = new DateOnly(year, 9, 1);
        var to = new DateOnly(year + 1, 8, 31);
        var fromUtc = ToUtc(from, zone);
        var toUtc = ToUtc(to.AddDays(1), zone);

        var catches = await db.Catches.AsNoTracking()
            .Where(c => c.UserId == userId && c.CaughtAt >= fromUtc && c.CaughtAt < toUtc)
            .ToListAsync(cancellationToken);
        var names = await db.Species.AsNoTracking().ToDictionaryAsync(s => s.Code, s => s.Name, cancellationToken);

        return Result.Success(SeasonStats.Build(year, from, to, catches, names, zone));
    }

    private static DateTimeOffset ToUtc(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }
}

internal static class SeasonStats
{
    public static SeasonStatsResponse Build(
        int seasonStartYear,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<CatchEntry> catches,
        IReadOnlyDictionary<string, string> speciesNames,
        TimeZoneInfo zone)
    {
        DateOnly LocalDay(CatchEntry c) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(c.CaughtAt, zone).DateTime);

        var bySpecies = catches
            .GroupBy(c => c.SpeciesCode)
            .Select(g => new SpeciesTotal(g.Key, speciesNames.GetValueOrDefault(g.Key, g.Key), Math.Round(g.Sum(c => c.WeightKg), 2), g.Count()))
            .OrderByDescending(s => s.WeightKg)
            .ToList();

        var byMonth = catches
            .GroupBy(c => (LocalDay(c).Year, LocalDay(c).Month))
            .Select(g => new MonthTotal(g.Key.Year, g.Key.Month, Math.Round(g.Sum(c => c.WeightKg), 2), g.Select(LocalDay).Distinct().Count()))
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToList();

        return new SeasonStatsResponse(
            $"{seasonStartYear}-{seasonStartYear + 1}",
            from,
            to,
            Math.Round(catches.Sum(c => c.WeightKg), 2),
            catches.Count,
            catches.Select(LocalDay).Distinct().Count(),
            bySpecies,
            byMonth);
    }
}
