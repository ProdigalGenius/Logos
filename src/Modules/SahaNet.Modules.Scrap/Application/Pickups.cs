using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Common;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Application.Market;
using SahaNet.Domain.Common;
using SahaNet.Domain.Identity;
using SahaNet.Modules.Scrap.Domain;
using SahaNet.Modules.Scrap.Persistence;

namespace SahaNet.Modules.Scrap.Application;

/// <summary>Adres yalnızca talep sahibine ve talebi üstlenen hurdacıya döner.</summary>
public sealed record PickupResponse(
    Guid Id,
    string ProvinceCode,
    string? ProvinceName,
    string District,
    string? Address,
    IReadOnlyList<string> Materials,
    decimal EstimatedKg,
    string? Notes,
    string Status,
    bool IsMine,
    bool IsMyJob,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClaimedAt);

internal static class PickupMapping
{
    public static PickupResponse ToResponse(this PickupRequest p, Guid viewerId) => new(
        p.Id, p.ProvinceCode, RegionCatalog.Find(p.ProvinceCode)?.Name, p.District,
        p.CanSeeAddress(viewerId) ? p.Address : null,
        p.Materials, p.EstimatedKg, p.Notes, p.Status.ToString(),
        p.RequesterId == viewerId, p.CollectorId == viewerId, p.CreatedAt, p.ClaimedAt);
}

public sealed record CreatePickupCommand(string ProvinceCode, string District, string Address, IReadOnlyList<string> Materials, decimal EstimatedKg, string? Notes)
    : IRequest<Result<PickupResponse>>;

internal sealed class CreatePickupCommandValidator : AbstractValidator<CreatePickupCommand>
{
    public CreatePickupCommandValidator()
    {
        RuleFor(x => x.ProvinceCode).NotEmpty().Length(2);
        RuleFor(x => x.District).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(PickupRequest.AddressMaxLength);
        RuleFor(x => x.Materials).NotEmpty().Must(m => m.Count <= 10);
        RuleFor(x => x.EstimatedKg).GreaterThan(0).LessThanOrEqualTo(100_000);
        RuleFor(x => x.Notes).MaximumLength(PickupRequest.NotesMaxLength);
    }
}

internal sealed class CreatePickupCommandHandler(ICurrentUser currentUser, ScrapDbContext db, IDateTimeProvider clock)
    : IRequestHandler<CreatePickupCommand, Result<PickupResponse>>
{
    private const int MaxOpen = 5;

    public async Task<Result<PickupResponse>> Handle(CreatePickupCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<PickupResponse>(IdentityErrors.Unauthenticated);
        }

        if (RegionCatalog.Find(request.ProvinceCode) is null)
        {
            return Result.Failure<PickupResponse>(IdentityErrors.RegionUnknown);
        }

        var materials = request.Materials.Distinct(StringComparer.Ordinal).ToList();
        if (await db.Materials.CountAsync(m => materials.Contains(m.Code), cancellationToken) != materials.Count)
        {
            return Result.Failure<PickupResponse>(ScrapErrors.MaterialUnknown);
        }

        if (await db.Pickups.CountAsync(p => p.RequesterId == userId && (p.Status == PickupStatus.Open || p.Status == PickupStatus.Claimed), cancellationToken) >= MaxOpen)
        {
            return Result.Failure<PickupResponse>(ScrapErrors.OpenLimit);
        }

        var pickup = PickupRequest.Create(userId, request.ProvinceCode, request.District, request.Address, materials, request.EstimatedKg, request.Notes, clock.UtcNow);
        db.Pickups.Add(pickup);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(pickup.ToResponse(userId));
    }
}

/// <summary>Hurdacılar için ildeki açık talepler (adres gizli).</summary>
public sealed record GetOpenPickupsQuery(string ProvinceCode) : IRequest<Result<IReadOnlyList<PickupResponse>>>;

internal sealed class GetOpenPickupsQueryHandler(ICurrentUser currentUser, ScrapDbContext db)
    : IRequestHandler<GetOpenPickupsQuery, Result<IReadOnlyList<PickupResponse>>>
{
    public async Task<Result<IReadOnlyList<PickupResponse>>> Handle(GetOpenPickupsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<IReadOnlyList<PickupResponse>>(IdentityErrors.Unauthenticated);
        }

        var open = await db.Pickups.AsNoTracking()
            .Where(p => p.Status == PickupStatus.Open && p.ProvinceCode == request.ProvinceCode)
            .OrderBy(p => p.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<PickupResponse>>(open.Select(p => p.ToResponse(userId)).ToList());
    }
}

/// <summary>Taleplerim (sahibi olduklarım) ve işlerim (üstlendiklerim).</summary>
public sealed record GetMyPickupsQuery : IRequest<Result<IReadOnlyList<PickupResponse>>>;

internal sealed class GetMyPickupsQueryHandler(ICurrentUser currentUser, ScrapDbContext db)
    : IRequestHandler<GetMyPickupsQuery, Result<IReadOnlyList<PickupResponse>>>
{
    public async Task<Result<IReadOnlyList<PickupResponse>>> Handle(GetMyPickupsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<IReadOnlyList<PickupResponse>>(IdentityErrors.Unauthenticated);
        }

        var mine = await db.Pickups.AsNoTracking()
            .Where(p => p.RequesterId == userId || p.CollectorId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<PickupResponse>>(mine.Select(p => p.ToResponse(userId)).ToList());
    }
}

public enum PickupAction
{
    Claim = 0,
    Release = 1,
    Complete = 2,
    Cancel = 3,
}

public sealed record PickupActionCommand(Guid Id, PickupAction Action) : IRequest<Result<PickupResponse>>;

internal sealed class PickupActionCommandHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    ScrapDbContext db,
    IUserNotifier notifier,
    IDateTimeProvider clock)
    : IRequestHandler<PickupActionCommand, Result<PickupResponse>>
{
    public async Task<Result<PickupResponse>> Handle(PickupActionCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<PickupResponse>(IdentityErrors.Unauthenticated);
        }

        var pickup = await db.Pickups.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (pickup is null)
        {
            return Result.Failure<PickupResponse>(ScrapErrors.PickupNotFound);
        }

        if (request.Action == PickupAction.Claim && await users.GetByIdAsync(userId, cancellationToken) is not { Status: UserStatus.Active })
        {
            return Result.Failure<PickupResponse>(IdentityErrors.AccountSuspended);
        }

        var now = clock.UtcNow;
        var result = request.Action switch
        {
            PickupAction.Claim => pickup.Claim(userId, now),
            PickupAction.Release => pickup.Release(userId),
            PickupAction.Complete => pickup.Complete(userId, now),
            _ => pickup.Cancel(userId, now),
        };
        if (result.IsFailure)
        {
            return Result.Failure<PickupResponse>(result.Error);
        }

        // İki hurdacı aynı anda üstlenmeye çalışırsa satır sürümü biri için 409 üretir.
        await db.SaveChangesAsync(cancellationToken);

        if (request.Action == PickupAction.Claim)
        {
            await notifier.NotifyAsync(
                pickup.RequesterId,
                "Hurdacı yolda",
                "Toplama talebiniz bir hurdacı tarafından üstlenildi.",
                new Dictionary<string, string> { ["type"] = "scrap.pickup", ["pickupId"] = pickup.Id.ToString() },
                cancellationToken);
        }

        return Result.Success(pickup.ToResponse(userId));
    }
}
