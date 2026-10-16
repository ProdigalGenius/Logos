using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Options;
using SahaNet.Modules.Fishing.Domain;
using SahaNet.Modules.Fishing.Persistence;

namespace SahaNet.Modules.Fishing.Application;

public sealed record SpeciesResponse(string Code, string Name, string? ScientificName, int? MinLengthCm);

public sealed record BanResponse(
    Guid Id,
    string Title,
    string Period,
    string? SpeciesCode,
    string? RegionCode,
    string? Gear,
    string? Notes,
    bool IsActiveToday);

internal static class CatalogMapping
{
    private static readonly string[] MonthNames =
        ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"];

    public static SpeciesResponse ToResponse(this Species s) => new(s.Code, s.Name, s.ScientificName, s.MinLengthCm);

    public static BanResponse ToResponse(this FishingBan b, DateOnly today) => new(
        b.Id,
        b.Title,
        $"{b.StartDay} {MonthNames[b.StartMonth - 1]} – {b.EndDay} {MonthNames[b.EndMonth - 1]}",
        b.SpeciesCode,
        b.RegionCode,
        b.Gear,
        b.Notes,
        b.IsActiveOn(today));

    public static DateOnly LocalToday(IDateTimeProvider clock, string zoneId) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(zoneId)).DateTime);
}

public sealed record GetSpeciesQuery : IRequest<IReadOnlyList<SpeciesResponse>>;

internal sealed class GetSpeciesQueryHandler(FishingDbContext db) : IRequestHandler<GetSpeciesQuery, IReadOnlyList<SpeciesResponse>>
{
    public async Task<IReadOnlyList<SpeciesResponse>> Handle(GetSpeciesQuery request, CancellationToken cancellationToken) =>
        (await db.Species.AsNoTracking().ToListAsync(cancellationToken))
        .OrderBy(s => s.Code == "diger")
        .ThenBy(s => s.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), false))
        .Select(s => s.ToResponse())
        .ToList();
}

/// <summary>Av yasakları takvimi; isteğe bağlı tür/bölge filtresi ve "yalnızca bugün yürürlükte" seçeneği.</summary>
public sealed record GetBansQuery(string? SpeciesCode, string? RegionCode, bool ActiveOnly) : IRequest<IReadOnlyList<BanResponse>>;

internal sealed class GetBansQueryHandler(FishingDbContext db, IDateTimeProvider clock, IOptions<AlertOptions> alertOptions)
    : IRequestHandler<GetBansQuery, IReadOnlyList<BanResponse>>
{
    public async Task<IReadOnlyList<BanResponse>> Handle(GetBansQuery request, CancellationToken cancellationToken)
    {
        var today = CatalogMapping.LocalToday(clock, alertOptions.Value.DisplayTimeZone);
        var bans = await db.Bans.AsNoTracking().ToListAsync(cancellationToken);

        return bans
            .Where(b => b.Applies(request.SpeciesCode, request.RegionCode))
            .Where(b => !request.ActiveOnly || b.IsActiveOn(today))
            .OrderByDescending(b => b.IsActiveOn(today))
            .ThenBy(b => (b.StartMonth * 100) + b.StartDay)
            .Select(b => b.ToResponse(today))
            .ToList();
    }
}
