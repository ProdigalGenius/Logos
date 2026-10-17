using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Application.Moderation;
using SahaNet.Domain.Common;
using SahaNet.Domain.Moderation;
using SahaNet.Modules.Fishing.Domain;
using SahaNet.Modules.Fishing.Persistence;

namespace SahaNet.Modules.Fishing.Application;

// ---- Yönetici: av yasakları ve asgari boylar (taslak seed verisinin resmi tebliğle güncellenmesi) ----

public sealed record SaveBanCommand(
    Guid? Id,
    string Title,
    int StartMonth,
    int StartDay,
    int EndMonth,
    int EndDay,
    string? SpeciesCode,
    string? RegionCode,
    string? Gear,
    string? Notes) : IRequest<Result<BanResponse>>;

internal sealed class SaveBanCommandValidator : AbstractValidator<SaveBanCommand>
{
    public SaveBanCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.StartMonth).InclusiveBetween(1, 12);
        RuleFor(x => x.EndMonth).InclusiveBetween(1, 12);
        RuleFor(x => x).Must(x => IsValidDay(x.StartMonth, x.StartDay) && IsValidDay(x.EndMonth, x.EndDay)).WithMessage("Geçersiz gün.");
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Gear).MaximumLength(64);
        RuleFor(x => x.RegionCode).MaximumLength(64);
    }

    private static bool IsValidDay(int month, int day) => month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(2024, month);
}

internal sealed class SaveBanCommandHandler(
    ICurrentUser currentUser,
    FishingDbContext db,
    IModerationService audit,
    IUnitOfWork coreUnitOfWork,
    IDateTimeProvider clock,
    Microsoft.Extensions.Options.IOptions<SahaNet.Application.Options.AlertOptions> alertOptions)
    : IRequestHandler<SaveBanCommand, Result<BanResponse>>
{
    public async Task<Result<BanResponse>> Handle(SaveBanCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAdmin)
        {
            return Result.Failure<BanResponse>(ModerationErrors.AdminOnly);
        }

        if (request.SpeciesCode is not null && !await db.Species.AnyAsync(s => s.Code == request.SpeciesCode, cancellationToken))
        {
            return Result.Failure<BanResponse>(FishingErrors.SpeciesUnknown);
        }

        FishingBan ban;
        if (request.Id is { } id)
        {
            var existing = await db.Bans.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
            if (existing is null)
            {
                return Result.Failure<BanResponse>(new Error("fishing.ban_not_found", "Yasak kaydı bulunamadı.", ErrorType.NotFound));
            }

            existing.Update(request.Title, request.StartMonth, request.StartDay, request.EndMonth, request.EndDay, request.SpeciesCode, request.RegionCode, request.Gear, request.Notes);
            ban = existing;
        }
        else
        {
            ban = new FishingBan(Guid.NewGuid(), request.Title, request.StartMonth, request.StartDay, request.EndMonth, request.EndDay,
                request.SpeciesCode, request.RegionCode, request.Gear, request.Notes);
            db.Bans.Add(ban);
        }

        await db.SaveChangesAsync(cancellationToken);
        audit.Audit(currentUser.UserId!.Value, request.Id is null ? "fishing.ban.create" : "fishing.ban.update", "FishingBan", ban.Id.ToString(), ban.Title);
        await coreUnitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(ban.ToResponse(CatalogMapping.LocalToday(clock, alertOptions.Value.DisplayTimeZone)));
    }
}

public sealed record DeleteBanCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteBanCommandHandler(ICurrentUser currentUser, FishingDbContext db, IModerationService audit, IUnitOfWork coreUnitOfWork)
    : IRequestHandler<DeleteBanCommand, Result>
{
    public async Task<Result> Handle(DeleteBanCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAdmin)
        {
            return Result.Failure(ModerationErrors.AdminOnly);
        }

        var ban = await db.Bans.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);
        if (ban is null)
        {
            return Result.Failure(new Error("fishing.ban_not_found", "Yasak kaydı bulunamadı.", ErrorType.NotFound));
        }

        db.Bans.Remove(ban);
        await db.SaveChangesAsync(cancellationToken);
        audit.Audit(currentUser.UserId!.Value, "fishing.ban.delete", "FishingBan", ban.Id.ToString(), ban.Title);
        await coreUnitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record SetSpeciesMinLengthCommand(string Code, int? MinLengthCm) : IRequest<Result<SpeciesResponse>>;

internal sealed class SetSpeciesMinLengthCommandValidator : AbstractValidator<SetSpeciesMinLengthCommand>
{
    public SetSpeciesMinLengthCommandValidator() => RuleFor(x => x.MinLengthCm).InclusiveBetween(1, 1000).When(x => x.MinLengthCm is not null);
}

internal sealed class SetSpeciesMinLengthCommandHandler(ICurrentUser currentUser, FishingDbContext db, IModerationService audit, IUnitOfWork coreUnitOfWork)
    : IRequestHandler<SetSpeciesMinLengthCommand, Result<SpeciesResponse>>
{
    public async Task<Result<SpeciesResponse>> Handle(SetSpeciesMinLengthCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAdmin)
        {
            return Result.Failure<SpeciesResponse>(ModerationErrors.AdminOnly);
        }

        var species = await db.Species.FirstOrDefaultAsync(s => s.Code == request.Code, cancellationToken);
        if (species is null)
        {
            return Result.Failure<SpeciesResponse>(FishingErrors.SpeciesUnknown);
        }

        var before = species.MinLengthCm;
        species.SetMinLength(request.MinLengthCm);
        await db.SaveChangesAsync(cancellationToken);
        audit.Audit(currentUser.UserId!.Value, "fishing.species.min_length", "Species", species.Code, $"{before?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"} → {request.MinLengthCm?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}");
        await coreUnitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(species.ToResponse());
    }
}

/// <summary>Gösterge paneline balıkçı metrikleri.</summary>
internal sealed class FishingAdminMetrics(FishingDbContext db) : IAdminMetricsSection
{
    public string Name => FishingModuleDescriptor.ModuleKey;

    public async Task<IReadOnlyDictionary<string, decimal>> GetAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var d30 = now.AddDays(-30);
        var catches = await db.Catches.AsNoTracking().Where(c => c.CaughtAt >= d30).Select(c => c.WeightKg).ToListAsync(cancellationToken);
        return new Dictionary<string, decimal>
        {
            ["catchEntries30Days"] = catches.Count,
            ["catchKg30Days"] = (decimal)Math.Round(catches.Sum(), 1),
            ["spots"] = await db.Spots.CountAsync(cancellationToken),
            ["priceReports30Days"] = await db.MarketPrices.CountAsync(p => p.CreatedAt >= d30, cancellationToken),
            ["bans"] = await db.Bans.CountAsync(cancellationToken),
        };
    }
}
