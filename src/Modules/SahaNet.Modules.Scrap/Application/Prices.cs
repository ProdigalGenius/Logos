using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Common;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Domain.Common;
using SahaNet.Domain.Identity;
using SahaNet.Modules.Scrap.Domain;
using SahaNet.Modules.Scrap.Persistence;

namespace SahaNet.Modules.Scrap.Application;

public sealed record MaterialResponse(string Code, string Name, string Group);

public sealed record ScrapDailyPrice(DateOnly Date, string MaterialCode, string MaterialName, string ProvinceCode, string? ProvinceName, decimal Median, decimal Min, decimal Max, int Reports);

public sealed record GetMaterialsQuery : IRequest<IReadOnlyList<MaterialResponse>>;

internal sealed class GetMaterialsQueryHandler(ScrapDbContext db) : IRequestHandler<GetMaterialsQuery, IReadOnlyList<MaterialResponse>>
{
    public async Task<IReadOnlyList<MaterialResponse>> Handle(GetMaterialsQuery request, CancellationToken cancellationToken) =>
        (await db.Materials.AsNoTracking().ToListAsync(cancellationToken))
        .OrderBy(m => m.Group, StringComparer.Ordinal)
        .ThenBy(m => m.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), false))
        .Select(m => new MaterialResponse(m.Code, m.Name, m.Group))
        .ToList();
}

public sealed record ReportScrapPriceCommand(string MaterialCode, string ProvinceCode, decimal PricePerKg) : IRequest<Result>;

internal sealed class ReportScrapPriceCommandValidator : AbstractValidator<ReportScrapPriceCommand>
{
    public ReportScrapPriceCommandValidator()
    {
        RuleFor(x => x.MaterialCode).NotEmpty().MaximumLength(32);
        RuleFor(x => x.ProvinceCode).NotEmpty().Length(2);
        RuleFor(x => x.PricePerKg).GreaterThan(0).LessThanOrEqualTo(10_000);
    }
}

internal sealed class ReportScrapPriceCommandHandler(ICurrentUser currentUser, ScrapDbContext db, IDateTimeProvider clock)
    : IRequestHandler<ReportScrapPriceCommand, Result>
{
    public async Task<Result> Handle(ReportScrapPriceCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(IdentityErrors.Unauthenticated);
        }

        if (RegionCatalog.Find(request.ProvinceCode) is null)
        {
            return Result.Failure(IdentityErrors.RegionUnknown);
        }

        if (!await db.Materials.AnyAsync(m => m.Code == request.MaterialCode, cancellationToken))
        {
            return Result.Failure(ScrapErrors.MaterialUnknown);
        }

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var existing = await db.Prices.FirstOrDefaultAsync(
            p => p.ReportedBy == userId && p.MaterialCode == request.MaterialCode && p.ProvinceCode == request.ProvinceCode && p.Date == today,
            cancellationToken);
        if (existing is not null)
        {
            db.Prices.Remove(existing);
        }

        db.Prices.Add(ScrapPriceReport.Create(request.MaterialCode, request.ProvinceCode, today, request.PricePerKg, userId, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record GetScrapPricesQuery(string? MaterialCode, string? ProvinceCode, int Days = 7) : IRequest<IReadOnlyList<ScrapDailyPrice>>;

internal sealed class GetScrapPricesQueryHandler(ScrapDbContext db, IDateTimeProvider clock) : IRequestHandler<GetScrapPricesQuery, IReadOnlyList<ScrapDailyPrice>>
{
    public async Task<IReadOnlyList<ScrapDailyPrice>> Handle(GetScrapPricesQuery request, CancellationToken cancellationToken)
    {
        var since = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime).AddDays(-(Math.Clamp(request.Days, 1, 30) - 1));
        var query = db.Prices.AsNoTracking().Where(p => p.Date >= since);
        if (request.MaterialCode is not null)
        {
            query = query.Where(p => p.MaterialCode == request.MaterialCode);
        }

        if (request.ProvinceCode is not null)
        {
            query = query.Where(p => p.ProvinceCode == request.ProvinceCode);
        }

        var reports = await query.ToListAsync(cancellationToken);
        var names = await db.Materials.AsNoTracking().ToDictionaryAsync(m => m.Code, m => m.Name, cancellationToken);
        return reports
            .GroupBy(r => (r.Date, r.MaterialCode, r.ProvinceCode))
            .Select(g =>
            {
                var sorted = g.Select(r => r.PricePerKg).Order().ToList();
                var median = sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : decimal.Round((sorted[(sorted.Count / 2) - 1] + sorted[sorted.Count / 2]) / 2, 2);
                return new ScrapDailyPrice(g.Key.Date, g.Key.MaterialCode, names.GetValueOrDefault(g.Key.MaterialCode, g.Key.MaterialCode),
                    g.Key.ProvinceCode, RegionCatalog.Find(g.Key.ProvinceCode)?.Name, median, sorted[0], sorted[^1], sorted.Count);
            })
            .OrderByDescending(p => p.Date)
            .ThenBy(p => p.MaterialName, StringComparer.Ordinal)
            .ToList();
    }
}
