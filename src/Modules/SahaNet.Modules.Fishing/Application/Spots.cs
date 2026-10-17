using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Domain.Common;
using SahaNet.Domain.Consents;
using SahaNet.Domain.Geo;
using SahaNet.Domain.Identity;
using SahaNet.Modules.Fishing.Domain;
using SahaNet.Modules.Fishing.Persistence;

namespace SahaNet.Modules.Fishing.Application;

public sealed record SpotResponse(
    Guid Id,
    string Name,
    double Latitude,
    double Longitude,
    double? DepthM,
    string? Notes,
    IReadOnlyList<string> Species,
    bool IsMine,
    int ShareCount,
    DateTimeOffset UpdatedAt);

internal static class SpotMapping
{
    public static SpotResponse ToResponse(this CatchSpot s, Guid viewerId) => new(
        s.Id,
        s.Name,
        s.Location.Latitude,
        s.Location.Longitude,
        s.DepthM,
        s.OwnerId == viewerId ? s.Notes : null,
        s.SpeciesList,
        s.OwnerId == viewerId,
        s.OwnerId == viewerId ? s.Shares.Count : 0,
        s.UpdatedAt);
}

// ---- Listele: kendi noktalarım + benimle paylaşılanlar ----

public sealed record GetMySpotsQuery : IRequest<Result<IReadOnlyList<SpotResponse>>>;

internal sealed class GetMySpotsQueryHandler(ICurrentUser currentUser, FishingDbContext db)
    : IRequestHandler<GetMySpotsQuery, Result<IReadOnlyList<SpotResponse>>>
{
    public async Task<Result<IReadOnlyList<SpotResponse>>> Handle(GetMySpotsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<IReadOnlyList<SpotResponse>>(IdentityErrors.Unauthenticated);
        }

        var spots = await db.Spots.AsNoTracking()
            .Where(s => s.OwnerId == userId || s.Shares.Any(x => x.UserId == userId))
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<SpotResponse>>(spots.Select(s => s.ToResponse(userId)).ToList());
    }
}

// ---- Kaydet / güncelle ----

public sealed record SaveSpotCommand(
    Guid? Id,
    string Name,
    double Latitude,
    double Longitude,
    double? DepthM,
    string? Notes,
    IReadOnlyList<string>? Species,
    Guid? ClientId = null) : IRequest<Result<SpotResponse>>;

internal sealed class SaveSpotCommandValidator : AbstractValidator<SaveSpotCommand>
{
    public SaveSpotCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(CatchSpot.NameMaxLength);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.DepthM).InclusiveBetween(0, 11_000).When(x => x.DepthM is not null);
        RuleFor(x => x.Notes).MaximumLength(CatchSpot.NotesMaxLength);
        RuleFor(x => x.Species).Must(s => s is null || s.Count <= 20);
    }
}

internal sealed class SaveSpotCommandHandler(
    ICurrentUser currentUser,
    FishingDbContext db,
    IDateTimeProvider clock,
    IOptions<FishingOptions> options)
    : IRequestHandler<SaveSpotCommand, Result<SpotResponse>>
{
    public async Task<Result<SpotResponse>> Handle(SaveSpotCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<SpotResponse>(IdentityErrors.Unauthenticated);
        }

        var location = GeoPoint.Create(request.Latitude, request.Longitude);
        if (location.IsFailure)
        {
            return Result.Failure<SpotResponse>(location.Error);
        }

        if (request.Species is { Count: > 0 } species)
        {
            var known = await db.Species.CountAsync(s => species.Contains(s.Code), cancellationToken);
            if (known != species.Distinct().Count())
            {
                return Result.Failure<SpotResponse>(FishingErrors.SpeciesUnknown);
            }
        }

        var now = clock.UtcNow;
        CatchSpot spot;
        if (request.Id is { } id)
        {
            var existing = await db.Spots.FirstOrDefaultAsync(s => s.Id == id && s.OwnerId == userId, cancellationToken);
            if (existing is null)
            {
                return Result.Failure<SpotResponse>(FishingErrors.SpotNotFound);
            }

            existing.Update(request.Name, location.Value, request.DepthM, request.Notes, request.Species, now);
            spot = existing;
        }
        else if (request.ClientId is { } clientId &&
                 await db.Spots.FirstOrDefaultAsync(s => s.Id == clientId, cancellationToken) is { } replayed)
        {
            // Çevrimdışı tekrar gönderim: aynı istemci kimliği → aynı nokta.
            if (replayed.OwnerId != userId)
            {
                return Result.Failure<SpotResponse>(FishingErrors.SpotNotFound);
            }

            spot = replayed;
        }
        else
        {
            var count = await db.Spots.CountAsync(s => s.OwnerId == userId, cancellationToken);
            if (count >= options.Value.FreeSpotLimit)
            {
                return Result.Failure<SpotResponse>(FishingErrors.SpotLimitReached);
            }

            spot = CatchSpot.Create(userId, request.Name, location.Value, request.DepthM, request.Notes, request.Species, now, request.ClientId);
            db.Spots.Add(spot);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(spot.ToResponse(userId));
    }
}

// ---- Sil ----

public sealed record DeleteSpotCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteSpotCommandHandler(ICurrentUser currentUser, FishingDbContext db) : IRequestHandler<DeleteSpotCommand, Result>
{
    public async Task<Result> Handle(DeleteSpotCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(IdentityErrors.Unauthenticated);
        }

        var spot = await db.Spots.FirstOrDefaultAsync(s => s.Id == request.Id && s.OwnerId == userId, cancellationToken);
        if (spot is null)
        {
            return Result.Failure(FishingErrors.SpotNotFound);
        }

        db.Spots.Remove(spot);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

// ---- Paylaş (telefon numarasıyla, konum paylaşımı rızası şart) ----

public sealed record ShareSpotCommand(Guid SpotId, string Phone) : IRequest<Result>;

internal sealed class ShareSpotCommandValidator : AbstractValidator<ShareSpotCommand>
{
    public ShareSpotCommandValidator() => RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
}

internal sealed class ShareSpotCommandHandler(
    ICurrentUser currentUser,
    FishingDbContext db,
    IUserRepository users,
    IConsentRepository consents,
    IPersonalDataProtector protector,
    IDateTimeProvider clock)
    : IRequestHandler<ShareSpotCommand, Result>
{
    public async Task<Result> Handle(ShareSpotCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(IdentityErrors.Unauthenticated);
        }

        var documents = await consents.GetActiveDocumentsAsync(cancellationToken);
        var given = await consents.GetUserConsentsAsync(userId, cancellationToken);
        if (!ConsentPolicy.IsGranted(ConsentType.LocationSharing, documents, given))
        {
            return Result.Failure(FishingErrors.LocationConsentRequired);
        }

        var phone = PhoneNumber.Create(request.Phone);
        if (phone.IsFailure)
        {
            return phone;
        }

        var spot = await db.Spots.FirstOrDefaultAsync(s => s.Id == request.SpotId && s.OwnerId == userId, cancellationToken);
        if (spot is null)
        {
            return Result.Failure(FishingErrors.SpotNotFound);
        }

        var target = await users.GetByPhoneHashAsync(protector.ComputeLookupHash(phone.Value.Value), cancellationToken);
        if (target is null || target.Status != UserStatus.Active)
        {
            return Result.Failure(FishingErrors.ShareTargetNotFound);
        }

        var result = spot.ShareWith(target.Id, clock.UtcNow);
        if (result.IsSuccess)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}

public sealed record UnshareSpotCommand(Guid SpotId, Guid UserId) : IRequest<Result>;

internal sealed class UnshareSpotCommandHandler(ICurrentUser currentUser, FishingDbContext db) : IRequestHandler<UnshareSpotCommand, Result>
{
    public async Task<Result> Handle(UnshareSpotCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(IdentityErrors.Unauthenticated);
        }

        // Sahip herkesle paylaşımı kaldırabilir; paylaşılan kişi kendini çıkarabilir.
        var spot = await db.Spots.FirstOrDefaultAsync(
            s => s.Id == request.SpotId && (s.OwnerId == userId || (request.UserId == userId && s.Shares.Any(x => x.UserId == userId))),
            cancellationToken);
        if (spot is null)
        {
            return Result.Failure(FishingErrors.SpotNotFound);
        }

        spot.Unshare(request.UserId);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
