using MediatR;
using Microsoft.Extensions.Options;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Alerts;
using SahaNet.Application.Consents;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Application.Identity.Profile;
using SahaNet.Application.Modules;
using SahaNet.Application.Options;
using SahaNet.Domain.Common;
using SahaNet.Domain.Identity;

namespace SahaNet.Application.Identity.Privacy;

// ---- KVKK md. 11: Verilerimi indir ----

public sealed record PersonalDataExport(
    DateTimeOffset GeneratedAt,
    ProfileResponse Profile,
    DateTimeOffset? LastLoginAt,
    IReadOnlyList<ConsentRecordResponse> Consents,
    IReadOnlyList<SubscriptionResponse> RegionSubscriptions,
    IReadOnlyList<ExportedDevice> Devices,
    IReadOnlyDictionary<string, object?> Modules);

/// <summary>Cihaz token'ı dışa aktarılmaz (gizli kimlik bilgisi); yalnızca platform ve tarihler.</summary>
public sealed record ExportedDevice(string Platform, DateTimeOffset RegisteredAt, DateTimeOffset LastSeenAt);

public sealed record ExportMyDataQuery : IRequest<Result<PersonalDataExport>>;

internal sealed class ExportMyDataQueryHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    IConsentRepository consents,
    ISubscriptionRepository subscriptions,
    IRegionRepository regions,
    IDeviceRepository devices,
    IEnumerable<IPersonalDataSection> moduleSections,
    IDateTimeProvider clock)
    : IRequestHandler<ExportMyDataQuery, Result<PersonalDataExport>>
{
    public async Task<Result<PersonalDataExport>> Handle(ExportMyDataQuery request, CancellationToken cancellationToken)
    {
        var userResult = await currentUser.LoadAsync(users, cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<PersonalDataExport>(userResult.Error);
        }

        var user = userResult.Value;
        var history = await consents.GetUserConsentsAsync(user.Id, cancellationToken);

        var modules = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var section in moduleSections)
        {
            modules[section.Name] = await section.ExportAsync(user.Id, cancellationToken);
        }

        return Result.Success(new PersonalDataExport(
            clock.UtcNow,
            user.ToResponse(),
            user.LastLoginAt,
            history.OrderBy(c => c.RecordedAt).Select(c => c.ToResponse()).ToList(),
            await SubscriptionViews.BuildAsync(user.Id, subscriptions, regions, cancellationToken),
            (await devices.GetByUserAsync(user.Id, cancellationToken))
                .Select(d => new ExportedDevice(d.Platform.ToString(), d.CreatedAt, d.LastSeenAt))
                .ToList(),
            modules));
    }
}

// ---- Hesabımı sil (30 gün bekleme) ----

public sealed record AccountDeletionResponse(DateTimeOffset ScheduledFor);

public sealed record RequestAccountDeletionCommand : IRequest<Result<AccountDeletionResponse>>;

internal sealed class RequestAccountDeletionCommandHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    IOptions<IdentityOptions> options)
    : IRequestHandler<RequestAccountDeletionCommand, Result<AccountDeletionResponse>>
{
    public async Task<Result<AccountDeletionResponse>> Handle(RequestAccountDeletionCommand request, CancellationToken cancellationToken)
    {
        var userResult = await currentUser.LoadAsync(users, cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<AccountDeletionResponse>(userResult.Error);
        }

        var now = clock.UtcNow;
        var user = userResult.Value;
        var result = user.RequestDeletion(now, options.Value.AccountDeletionGracePeriod);
        if (result.IsFailure)
        {
            return Result.Failure<AccountDeletionResponse>(result.Error);
        }

        // Tüm cihazlardaki oturumlar kapanır; kullanıcı tekrar giriş yapıp talebi iptal edebilir.
        foreach (var token in await refreshTokens.GetActiveByUserAsync(user.Id, cancellationToken))
        {
            token.Revoke(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new AccountDeletionResponse(result.Value));
    }
}

public sealed record CancelAccountDeletionCommand : IRequest<Result>;

internal sealed class CancelAccountDeletionCommandHandler(ICurrentUser currentUser, IUserRepository users, IUnitOfWork unitOfWork)
    : IRequestHandler<CancelAccountDeletionCommand, Result>
{
    public async Task<Result> Handle(CancelAccountDeletionCommand request, CancellationToken cancellationToken)
    {
        var userResult = await currentUser.LoadAsync(users, cancellationToken);
        if (userResult.IsFailure)
        {
            return userResult;
        }

        var result = userResult.Value.CancelDeletion();
        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}

// ---- Kalıcı silme işi (Faz 2'de Hangfire ile günlük çalıştırılacak) ----

public sealed record PurgeDeletedAccountsCommand : IRequest<int>;

internal sealed class PurgeDeletedAccountsCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    IOptions<IdentityOptions> options)
    : IRequestHandler<PurgeDeletedAccountsCommand, int>
{
    public async Task<int> Handle(PurgeDeletedAccountsCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var grace = options.Value.AccountDeletionGracePeriod;
        var due = await users.GetPendingDeletionRequestedBeforeAsync(now - grace, cancellationToken);

        foreach (var user in due.Where(u => u.IsDueForPurge(now, grace)))
        {
            // Modüller UserPurged event'ini dinleyip kendi verilerini siler.
            user.MarkPurged();
            users.Remove(user);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return due.Count;
    }
}
