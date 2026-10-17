using SahaNet.Domain.Common;

namespace SahaNet.Domain.Moderation;

public enum ReportTargetType
{
    Listing = 0,
    User = 1,
    Message = 2,
}

public enum ReportReason
{
    Spam = 0,
    Fraud = 1,
    Inappropriate = 2,

    /// <summary>Yasak dönemde veya asgari boyun altında av satışı.</summary>
    IllegalCatch = 3,
    Other = 4,
}

public enum ReportStatus
{
    Open = 0,
    ActionTaken = 1,
    Dismissed = 2,
}

public enum ModerationAction
{
    Dismiss = 0,
    RemoveContent = 1,
    SuspendUser = 2,
}

public static class ModerationErrors
{
    public static readonly Error ReportNotFound = new("moderation.report_not_found", "Şikâyet bulunamadı.", ErrorType.NotFound);
    public static readonly Error TargetNotFound = new("moderation.target_not_found", "Şikâyet edilen içerik bulunamadı.", ErrorType.NotFound);
    public static readonly Error AlreadyReported = new("moderation.already_reported", "Bu içeriği zaten şikâyet ettiniz.", ErrorType.Conflict);
    public static readonly Error AlreadyResolved = new("moderation.already_resolved", "Şikâyet zaten sonuçlandı.", ErrorType.Conflict);
    public static readonly Error DailyLimit = new("moderation.daily_limit", "Bugün için şikâyet sınırına ulaştınız.", ErrorType.TooManyRequests);
    public static readonly Error ModeratorOnly = new("moderation.moderator_only", "Bu işlem moderatörlere açık.", ErrorType.Forbidden);
    public static readonly Error AdminOnly = new("moderation.admin_only", "Bu işlem yöneticiye açık.", ErrorType.Forbidden);
    public static readonly Error SpamBlocked = new("moderation.spam_blocked", "İçerik otomatik spam filtresine takıldı. Linkleri ve tekrarları kaldırıp tekrar deneyin.", ErrorType.Validation);
    public static readonly Error ActionNotApplicable = new("moderation.action_not_applicable", "Bu işlem bu içerik türüne uygulanamaz.", ErrorType.Validation);
}

/// <summary>Kullanıcı veya sistem (spam filtresi) şikâyeti; moderatör kuyruğunda bekler.</summary>
public sealed class ContentReport : AggregateRoot<Guid>
{
    public const int NoteMaxLength = 1000;

    private ContentReport(Guid id)
        : base(id)
    {
    }

    // EF Core
    private ContentReport()
        : this(Guid.Empty)
    {
    }

    /// <summary>Sistem (spam filtresi) şikâyetlerinde boş.</summary>
    public Guid? ReporterId { get; private set; }

    public ReportTargetType TargetType { get; private set; }

    public Guid TargetId { get; private set; }

    /// <summary>İçeriğin sahibi (askıya alma kararı için).</summary>
    public Guid? TargetOwnerId { get; private set; }

    public ReportReason Reason { get; private set; }

    public string? Note { get; private set; }

    public int? SpamScore { get; private set; }

    public ReportStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public Guid? ResolvedBy { get; private set; }

    public ModerationAction? Action { get; private set; }

    public string? ResolutionNote { get; private set; }

    public static ContentReport ByUser(Guid reporterId, ReportTargetType type, Guid targetId, Guid? ownerId, ReportReason reason, string? note, DateTimeOffset now) =>
        new(Guid.NewGuid())
        {
            ReporterId = reporterId,
            TargetType = type,
            TargetId = targetId,
            TargetOwnerId = ownerId,
            Reason = reason,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            Status = ReportStatus.Open,
            CreatedAt = now,
        };

    public static ContentReport BySpamFilter(ReportTargetType type, Guid targetId, Guid ownerId, int score, string details, DateTimeOffset now) =>
        new(Guid.NewGuid())
        {
            TargetType = type,
            TargetId = targetId,
            TargetOwnerId = ownerId,
            Reason = ReportReason.Spam,
            Note = details,
            SpamScore = score,
            Status = ReportStatus.Open,
            CreatedAt = now,
        };

    public Result Resolve(Guid moderatorId, ModerationAction action, string? note, DateTimeOffset now)
    {
        if (Status != ReportStatus.Open)
        {
            return Result.Failure(ModerationErrors.AlreadyResolved);
        }

        Status = action == ModerationAction.Dismiss ? ReportStatus.Dismissed : ReportStatus.ActionTaken;
        Action = action;
        ResolvedBy = moderatorId;
        ResolvedAt = now;
        ResolutionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return Result.Success();
    }
}

/// <summary>Yönetici/moderatör işlemlerinin değiştirilemez kaydı (hesap verebilirlik).</summary>
public sealed class AuditEntry : Entity<Guid>
{
    private AuditEntry(Guid id, Guid actorId, string action, string targetType, string targetId, string? details, DateTimeOffset at)
        : base(id)
    {
        ActorId = actorId;
        Action = action;
        TargetType = targetType;
        TargetId = targetId;
        Details = details;
        At = at;
    }

    // EF Core
    private AuditEntry()
        : base(Guid.Empty)
    {
        Action = null!;
        TargetType = null!;
        TargetId = null!;
    }

    public Guid ActorId { get; private set; }

    public string Action { get; private set; }

    public string TargetType { get; private set; }

    public string TargetId { get; private set; }

    public string? Details { get; private set; }

    public DateTimeOffset At { get; private set; }

    public static AuditEntry Record(Guid actorId, string action, string targetType, string targetId, string? details, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        return new AuditEntry(Guid.NewGuid(), actorId, action, targetType, targetId, details is { Length: > 2000 } ? details[..2000] : details, at);
    }
}
