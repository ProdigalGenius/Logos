using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Common;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Application.Options;
using SahaNet.Domain.Common;
using SahaNet.Domain.Identity;
using SahaNet.Modules.Fishing.Domain;
using SahaNet.Modules.Fishing.Persistence;

namespace SahaNet.Modules.Fishing.Application;

public sealed record DailyPrice(
    DateOnly Date,
    string SpeciesCode,
    string SpeciesName,
    string ProvinceCode,
    string? ProvinceName,
    decimal Median,
    decimal Min,
    decimal Max,
    int Reports);

/// <summary>Kullanıcı hal fiyatı bildirir (aynı kişi aynı gün/tür/il için tek kayıt; yenisi eskisinin yerini alır).</summary>
public sealed record ReportMarketPriceCommand(string SpeciesCode, string ProvinceCode, decimal PricePerKg, DateOnly? Date) : IRequest<Result>;

internal sealed class ReportMarketPriceCommandValidator : AbstractValidator<ReportMarketPriceCommand>
{
    public ReportMarketPriceCommandValidator()
    {
        RuleFor(x => x.SpeciesCode).NotEmpty().MaximumLength(32);
        RuleFor(x => x.ProvinceCode).NotEmpty().Length(2);
        RuleFor(x => x.PricePerKg).GreaterThan(0).LessThanOrEqualTo(100_000);
    }
}

internal sealed class ReportMarketPriceCommandHandler(
    ICurrentUser currentUser,
    FishingDbContext db,
    IDateTimeProvider clock,
    IOptions<AlertOptions> alertOptions)
    : IRequestHandler<ReportMarketPriceCommand, Result>
{
    public async Task<Result> Handle(ReportMarketPriceCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(IdentityErrors.Unauthenticated);
        }

        if (RegionCatalog.Find(request.ProvinceCode) is null)
        {
            return Result.Failure(IdentityErrors.RegionUnknown);
        }

        if (!await db.Species.AnyAsync(s => s.Code == request.SpeciesCode, cancellationToken))
        {
            return Result.Failure(FishingErrors.SpeciesUnknown);
        }

        var today = CatalogMapping.LocalToday(clock, alertOptions.Value.DisplayTimeZone);
        var date = request.Date ?? today;
        if (date > today || date < today.AddDays(-7))
        {
            return Result.Failure(new Error("fishing.price_date", "Fiyat yalnızca son 7 gün için girilebilir.", ErrorType.Validation));
        }

        var existing = await db.MarketPrices.FirstOrDefaultAsync(
            p => p.ReportedBy == userId && p.SpeciesCode == request.SpeciesCode && p.ProvinceCode == request.ProvinceCode && p.Date == date,
            cancellationToken);
        if (existing is not null)
        {
            db.MarketPrices.Remove(existing);
        }

        db.MarketPrices.Add(MarketPrice.Report(request.SpeciesCode, request.ProvinceCode, date, request.PricePerKg, userId, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record GetMarketPricesQuery(string? SpeciesCode, string? ProvinceCode, int Days = 7) : IRequest<IReadOnlyList<DailyPrice>>;

internal sealed class GetMarketPricesQueryValidator : AbstractValidator<GetMarketPricesQuery>
{
    public GetMarketPricesQueryValidator() => RuleFor(x => x.Days).InclusiveBetween(1, 30);
}

internal sealed class GetMarketPricesQueryHandler(FishingDbContext db, IDateTimeProvider clock, IOptions<AlertOptions> alertOptions)
    : IRequestHandler<GetMarketPricesQuery, IReadOnlyList<DailyPrice>>
{
    public async Task<IReadOnlyList<DailyPrice>> Handle(GetMarketPricesQuery request, CancellationToken cancellationToken)
    {
        var since = CatalogMapping.LocalToday(clock, alertOptions.Value.DisplayTimeZone).AddDays(-(request.Days - 1));
        var query = db.MarketPrices.AsNoTracking().Where(p => p.Date >= since);
        if (request.SpeciesCode is not null)
        {
            query = query.Where(p => p.SpeciesCode == request.SpeciesCode);
        }

        if (request.ProvinceCode is not null)
        {
            query = query.Where(p => p.ProvinceCode == request.ProvinceCode);
        }

        var reports = await query.ToListAsync(cancellationToken);
        var names = await db.Species.AsNoTracking().ToDictionaryAsync(s => s.Code, s => s.Name, cancellationToken);
        return PriceAggregation.Daily(reports, names);
    }
}

internal static class PriceAggregation
{
    /// <summary>Medyan: tek bir abartılı bildirim ortalamayı bozmasın diye.</summary>
    public static IReadOnlyList<DailyPrice> Daily(IEnumerable<MarketPrice> reports, IReadOnlyDictionary<string, string> speciesNames) =>
        reports
            .GroupBy(r => (r.Date, r.SpeciesCode, r.ProvinceCode))
            .Select(g =>
            {
                var prices = g.Select(r => r.PricePerKg).Order().ToList();
                return new DailyPrice(
                    g.Key.Date,
                    g.Key.SpeciesCode,
                    speciesNames.GetValueOrDefault(g.Key.SpeciesCode, g.Key.SpeciesCode),
                    g.Key.ProvinceCode,
                    RegionCatalog.Find(g.Key.ProvinceCode)?.Name,
                    Median(prices),
                    prices[0],
                    prices[^1],
                    prices.Count);
            })
            .OrderByDescending(p => p.Date)
            .ThenBy(p => p.SpeciesName, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), false))
            .ToList();

    internal static decimal Median(IReadOnlyList<decimal> sorted) =>
        sorted.Count % 2 == 1
            ? sorted[sorted.Count / 2]
            : decimal.Round((sorted[(sorted.Count / 2) - 1] + sorted[sorted.Count / 2]) / 2, 2);
}
