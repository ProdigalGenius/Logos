using FluentValidation;
using MediatR;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Common;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Application.Market;
using SahaNet.Domain.Common;
using SahaNet.Domain.Identity;
using SahaNet.Domain.Moderation;

namespace SahaNet.Application.Moderation;

public sealed record ReportResponse(
    Guid Id,
    string TargetType,
    Guid TargetId,
    Guid? TargetOwnerId,
    string? TargetPreview,
    string Reason,
    string? Note,
    int? SpamScore,
    bool BySystem,
    string Status,
    DateTimeOffset CreatedAt,
    string? Action,
    string? ResolutionNote,
    int OpenReportsOnTarget);

public sealed record ReportPage(IReadOnlyList<ReportResponse> Items, int Page, int PageSize, int Total);

public sealed record AdminUserResponse(
    Guid Id,
    string PhoneMasked,
    string? DisplayName,
    string? NicheKey,
    string? RegionName,
    string Status,
    string Role,
    string? SuspensionReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

public sealed record AuditResponse(Guid ActorId, string Action, string TargetType, string TargetId, string? Details, DateTimeOffset At);

internal static class ModerationGuards
{
    public static Error? RequireModerator(this ICurrentUser user) =>
        user.UserId is null || !user.IsModerator ? ModerationErrors.ModeratorOnly : null;

    public static Error? RequireAdmin(this ICurrentUser user) =>
        user.UserId is null || !user.IsAdmin ? ModerationErrors.AdminOnly : null;

    /// <summary>Yönetici görünümünde telefon maskelenir (KVKK: en az erişim).</summary>
    public static AdminUserResponse ToAdminResponse(this User u) => new(
        u.Id, u.Phone.Masked, u.DisplayName, u.NicheKey, RegionCatalog.Find(u.RegionCode)?.Name,
        u.Status.ToString(), u.Role.ToString(), u.SuspensionReason, u.CreatedAt, u.LastLoginAt);
}

// ---- Kullanıcı şikâyeti ----

public sealed record ReportContentCommand(string TargetType, Guid TargetId, string Reason, string? Note) : IRequest<Result<Guid>>;

internal sealed class ReportContentCommandValidator : AbstractValidator<ReportContentCommand>
{
    public ReportContentCommandValidator()
    {
        RuleFor(x => x.TargetType).Must(t => Enum.TryParse<ReportTargetType>(t, true, out _)).WithMessage("Hedef Listing, User veya Message olmalı.");
        RuleFor(x => x.Reason).Must(r => Enum.TryParse<ReportReason>(r, true, out _)).WithMessage("Geçersiz şikâyet sebebi.");
        RuleFor(x => x.TargetId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(ContentReport.NoteMaxLength);
    }
}

internal sealed class ReportContentCommandHandler(
    ICurrentUser currentUser,
    IModerationRepository moderation,
    IListingRepository listings,
    IConversationRepository conversations,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<ReportContentCommand, Result<Guid>>
{
    private const int DailyLimit = 20;

    public async Task<Result<Guid>> Handle(ReportContentCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<Guid>(IdentityErrors.Unauthenticated);
        }

        var type = Enum.Parse<ReportTargetType>(request.TargetType, true);
        var now = clock.UtcNow;
        if (await moderation.CountReportsByUserSinceAsync(userId, now.AddDays(-1), cancellationToken) >= DailyLimit)
        {
            return Result.Failure<Guid>(ModerationErrors.DailyLimit);
        }

        if (await moderation.HasOpenReportAsync(userId, type, request.TargetId, cancellationToken))
        {
            return Result.Failure<Guid>(ModerationErrors.AlreadyReported);
        }

        Guid? owner = type switch
        {
            ReportTargetType.Listing => (await listings.GetByIdAsync(request.TargetId, cancellationToken))?.SellerId,
            ReportTargetType.Message => (await conversations.GetMessageAsync(request.TargetId, cancellationToken))?.SenderId,
            _ => (await users.GetByIdAsync(request.TargetId, cancellationToken))?.Id,
        };
        if (owner is null)
        {
            return Result.Failure<Guid>(ModerationErrors.TargetNotFound);
        }

        var report = ContentReport.ByUser(userId, type, request.TargetId, owner, Enum.Parse<ReportReason>(request.Reason, true), request.Note, now);
        moderation.Add(report);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(report.Id);
    }
}

// ---- Moderatör kuyruğu ----

public sealed record GetReportsQuery(string? Status, int Page = 1, int PageSize = 25) : IRequest<Result<ReportPage>>;

internal sealed class GetReportsQueryHandler(
    ICurrentUser currentUser,
    IModerationRepository moderation,
    IListingRepository listings,
    IConversationRepository conversations,
    IUserRepository users)
    : IRequestHandler<GetReportsQuery, Result<ReportPage>>
{
    public async Task<Result<ReportPage>> Handle(GetReportsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.RequireModerator() is { } denied)
        {
            return Result.Failure<ReportPage>(denied);
        }

        ReportStatus? status = Enum.TryParse<ReportStatus>(request.Status, true, out var s) ? s : request.Status is null ? ReportStatus.Open : null;
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var (items, total) = await moderation.GetReportsAsync(status, Math.Max(1, request.Page), pageSize, cancellationToken);

        var responses = new List<ReportResponse>(items.Count);
        foreach (var r in items)
        {
            var preview = r.TargetType switch
            {
                ReportTargetType.Listing => (await listings.GetByIdAsync(r.TargetId, cancellationToken)) is { } l ? $"{l.Title} ({l.Status})" : null,
                ReportTargetType.Message => (await conversations.GetMessageAsync(r.TargetId, cancellationToken))?.Text,
                _ => (await users.GetByIdAsync(r.TargetId, cancellationToken)) is { } u ? $"{u.DisplayName ?? "(adsız)"} · {u.Phone.Masked}" : null,
            };
            var open = (await moderation.GetOpenReportsForTargetAsync(r.TargetType, r.TargetId, cancellationToken)).Count;
            responses.Add(new ReportResponse(
                r.Id, r.TargetType.ToString(), r.TargetId, r.TargetOwnerId, preview, r.Reason.ToString(), r.Note, r.SpamScore,
                r.ReporterId is null, r.Status.ToString(), r.CreatedAt, r.Action?.ToString(), r.ResolutionNote, open));
        }

        return Result.Success(new ReportPage(responses, request.Page, pageSize, total));
    }
}

// ---- Karar: reddet / içeriği kaldır / kullanıcıyı askıya al ----

public sealed record ResolveReportCommand(Guid ReportId, string Action, string? Note) : IRequest<Result>;

internal sealed class ResolveReportCommandValidator : AbstractValidator<ResolveReportCommand>
{
    public ResolveReportCommandValidator()
    {
        RuleFor(x => x.Action).Must(a => Enum.TryParse<ModerationAction>(a, true, out _)).WithMessage("İşlem Dismiss, RemoveContent veya SuspendUser olmalı.");
        RuleFor(x => x.Note).MaximumLength(ContentReport.NoteMaxLength);
    }
}

internal sealed class ResolveReportCommandHandler(
    ICurrentUser currentUser,
    IModerationRepository moderation,
    IModerationService audit,
    IListingRepository listings,
    IConversationRepository conversations,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<ResolveReportCommand, Result>
{
    public async Task<Result> Handle(ResolveReportCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.RequireModerator() is { } denied)
        {
            return Result.Failure(denied);
        }

        var moderatorId = currentUser.UserId!.Value;
        var report = await moderation.GetReportAsync(request.ReportId, cancellationToken);
        if (report is null)
        {
            return Result.Failure(ModerationErrors.ReportNotFound);
        }

        if (report.Status != ReportStatus.Open)
        {
            return Result.Failure(ModerationErrors.AlreadyResolved);
        }

        var action = Enum.Parse<ModerationAction>(request.Action, true);
        var now = clock.UtcNow;
        var reason = request.Note ?? $"Şikâyet: {report.Reason}";

        var applied = action switch
        {
            ModerationAction.RemoveContent => await RemoveContentAsync(report, reason, now, cancellationToken),
            ModerationAction.SuspendUser => await SuspendOwnerAsync(report, reason, now, cancellationToken),
            _ => Result.Success(),
        };
        if (applied.IsFailure)
        {
            return applied;
        }

        // Aynı içerik için açık tüm şikâyetler birlikte sonuçlanır.
        report.Resolve(moderatorId, action, request.Note, now);
        foreach (var open in await moderation.GetOpenReportsForTargetAsync(report.TargetType, report.TargetId, cancellationToken))
        {
            if (open.Id != report.Id)
            {
                open.Resolve(moderatorId, action, request.Note, now);
            }
        }
        audit.Audit(moderatorId, $"report.{action}", report.TargetType.ToString(), report.TargetId.ToString(), request.Note);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result> RemoveContentAsync(ContentReport report, string reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        switch (report.TargetType)
        {
            case ReportTargetType.Listing:
                var listing = await listings.GetByIdAsync(report.TargetId, cancellationToken);
                if (listing is null)
                {
                    return Result.Failure(ModerationErrors.TargetNotFound);
                }

                // Zaten kapalıysa yeniden kaldırma gerekmez.
                listing.RemoveByModerator(reason, now);
                return Result.Success();
            case ReportTargetType.Message:
                var message = await conversations.GetMessageAsync(report.TargetId, cancellationToken);
                if (message is null)
                {
                    return Result.Failure(ModerationErrors.TargetNotFound);
                }

                message.Redact();
                return Result.Success();
            default:
                return Result.Failure(ModerationErrors.ActionNotApplicable);
        }
    }

    private async Task<Result> SuspendOwnerAsync(ContentReport report, string reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (report.TargetOwnerId is not { } ownerId || await users.GetByIdAsync(ownerId, cancellationToken) is not { } user)
        {
            return Result.Failure(ModerationErrors.TargetNotFound);
        }

        if (user.Status != UserStatus.Suspended)
        {
            user.Suspend(reason);
        }

        if (report.TargetType != ReportTargetType.User)
        {
            await RemoveContentAsync(report, reason, now, cancellationToken);
        }

        return Result.Success();
    }
}

// ---- Kullanıcı yönetimi ----

public sealed record SearchUsersQuery(string? Phone, string? Text, string? Status, int Page = 1, int PageSize = 25) : IRequest<Result<IReadOnlyList<AdminUserResponse>>>;

internal sealed class SearchUsersQueryHandler(ICurrentUser currentUser, IUserRepository users, IPersonalDataProtector protector)
    : IRequestHandler<SearchUsersQuery, Result<IReadOnlyList<AdminUserResponse>>>
{
    public async Task<Result<IReadOnlyList<AdminUserResponse>>> Handle(SearchUsersQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.RequireModerator() is { } denied)
        {
            return Result.Failure<IReadOnlyList<AdminUserResponse>>(denied);
        }

        string? phoneHash = null;
        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            var phone = PhoneNumber.Create(request.Phone);
            if (phone.IsFailure)
            {
                return Result.Failure<IReadOnlyList<AdminUserResponse>>(phone.Error);
            }

            phoneHash = protector.ComputeLookupHash(phone.Value.Value);
        }

        UserStatus? status = Enum.TryParse<UserStatus>(request.Status, true, out var st) ? st : null;
        var (items, _) = await users.SearchAsync(phoneHash, request.Text, status, Math.Max(1, request.Page), Math.Clamp(request.PageSize, 1, 100), cancellationToken);
        return Result.Success<IReadOnlyList<AdminUserResponse>>(items.Select(u => u.ToAdminResponse()).ToList());
    }
}

public sealed record SetUserSuspensionCommand(Guid UserId, bool Suspend, string? Reason) : IRequest<Result>;

internal sealed class SetUserSuspensionCommandHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    IModerationService audit,
    IUnitOfWork unitOfWork)
    : IRequestHandler<SetUserSuspensionCommand, Result>
{
    public async Task<Result> Handle(SetUserSuspensionCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.RequireModerator() is { } denied)
        {
            return Result.Failure(denied);
        }

        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(IdentityErrors.UserNotFound);
        }

        // Moderatör yöneticiyi askıya alamaz.
        if (user.Role == UserRole.Admin && !currentUser.IsAdmin)
        {
            return Result.Failure(ModerationErrors.AdminOnly);
        }

        var result = request.Suspend ? user.Suspend(request.Reason ?? "Moderasyon kararı") : user.Unsuspend();
        if (result.IsFailure)
        {
            return result;
        }

        audit.Audit(currentUser.UserId!.Value, request.Suspend ? "user.suspend" : "user.unsuspend", "User", user.Id.ToString(), request.Reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>Askıya alınan kullanıcının aktif ilanları yayından kalkar.</summary>
internal sealed class RemoveListingsOnUserSuspended(IListingRepository listings, IUnitOfWork unitOfWork, IDateTimeProvider clock)
    : INotificationHandler<SahaNet.Application.Events.DomainEventNotification<UserSuspended>>
{
    public async Task Handle(SahaNet.Application.Events.DomainEventNotification<UserSuspended> notification, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var removed = false;
        foreach (var summary in await listings.GetBySellerAsync(notification.DomainEvent.UserId, cancellationToken))
        {
            if (summary.Status == Domain.Market.ListingStatus.Active && await listings.GetByIdAsync(summary.Id, cancellationToken) is { } tracked)
            {
                tracked.RemoveByModerator("Hesap askıya alındı", now);
                removed = true;
            }
        }

        if (removed)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed record SetUserRoleCommand(Guid UserId, string Role) : IRequest<Result>;

internal sealed class SetUserRoleCommandValidator : AbstractValidator<SetUserRoleCommand>
{
    public SetUserRoleCommandValidator() =>
        RuleFor(x => x.Role).Must(r => Enum.TryParse<UserRole>(r, true, out _)).WithMessage("Rol User, Moderator veya Admin olmalı.");
}

internal sealed class SetUserRoleCommandHandler(ICurrentUser currentUser, IUserRepository users, IModerationService audit, IUnitOfWork unitOfWork)
    : IRequestHandler<SetUserRoleCommand, Result>
{
    public static readonly Error CannotDemoteSelf = new("moderation.demote_self", "Kendi yönetici rolünüzü kaldıramazsınız.", ErrorType.Validation);

    public async Task<Result> Handle(SetUserRoleCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.RequireAdmin() is { } denied)
        {
            return Result.Failure(denied);
        }

        var role = Enum.Parse<UserRole>(request.Role, true);
        if (request.UserId == currentUser.UserId && role != UserRole.Admin)
        {
            return Result.Failure(CannotDemoteSelf);
        }

        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(IdentityErrors.UserNotFound);
        }

        user.AssignRole(role);
        audit.Audit(currentUser.UserId!.Value, "user.role", "User", user.Id.ToString(), role.ToString());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

// ---- Denetim kaydı ve gösterge paneli ----

public sealed record GetAuditLogQuery(int Limit = 100) : IRequest<Result<IReadOnlyList<AuditResponse>>>;

internal sealed class GetAuditLogQueryHandler(ICurrentUser currentUser, IModerationRepository moderation)
    : IRequestHandler<GetAuditLogQuery, Result<IReadOnlyList<AuditResponse>>>
{
    public async Task<Result<IReadOnlyList<AuditResponse>>> Handle(GetAuditLogQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.RequireAdmin() is { } denied)
        {
            return Result.Failure<IReadOnlyList<AuditResponse>>(denied);
        }

        var entries = await moderation.GetAuditAsync(Math.Clamp(request.Limit, 1, 500), cancellationToken);
        return Result.Success<IReadOnlyList<AuditResponse>>(entries.Select(e => new AuditResponse(e.ActorId, e.Action, e.TargetType, e.TargetId, e.Details, e.At)).ToList());
    }
}

public sealed record DashboardResponse(CoreMetrics Core, IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>> Modules, DateTimeOffset GeneratedAt);

public sealed record GetDashboardQuery : IRequest<Result<DashboardResponse>>;

internal sealed class GetDashboardQueryHandler(
    ICurrentUser currentUser,
    IAdminMetrics metrics,
    IEnumerable<IAdminMetricsSection> sections,
    IDateTimeProvider clock)
    : IRequestHandler<GetDashboardQuery, Result<DashboardResponse>>
{
    public async Task<Result<DashboardResponse>> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.RequireModerator() is { } denied)
        {
            return Result.Failure<DashboardResponse>(denied);
        }

        var now = clock.UtcNow;
        var modules = new SortedDictionary<string, IReadOnlyDictionary<string, decimal>>(StringComparer.Ordinal);
        foreach (var section in sections)
        {
            modules[section.Name] = await section.GetAsync(now, cancellationToken);
        }

        return Result.Success(new DashboardResponse(await metrics.GetAsync(now, cancellationToken), modules, now));
    }
}
