using SahaNet.Domain.Moderation;

namespace SahaNet.Application.Moderation;

public interface IModerationRepository
{
    Task<ContentReport?> GetReportAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ContentReport>> GetOpenReportsForTargetAsync(ReportTargetType type, Guid targetId, CancellationToken cancellationToken);

    Task<bool> HasOpenReportAsync(Guid reporterId, ReportTargetType type, Guid targetId, CancellationToken cancellationToken);

    Task<int> CountReportsByUserSinceAsync(Guid reporterId, DateTimeOffset since, CancellationToken cancellationToken);

    Task<(IReadOnlyList<ContentReport> Items, int Total)> GetReportsAsync(ReportStatus? status, int page, int pageSize, CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditEntry>> GetAuditAsync(int limit, CancellationToken cancellationToken);

    void Add(ContentReport report);

    void Add(AuditEntry entry);
}

/// <summary>Spam filtresi ve modüller için ortak işaretleme + denetim kaydı.</summary>
public interface IModerationService
{
    Task FlagAsync(ReportTargetType type, Guid targetId, Guid ownerId, SpamResult spam, CancellationToken cancellationToken);

    /// <summary>Denetim kaydı ekler (kaydetme çağıranın SaveChanges'ı ile).</summary>
    void Audit(Guid actorId, string action, string targetType, string targetId, string? details);
}

/// <summary>Gösterge paneli metrikleri (çekirdek). Infrastructure SQL ile uygular.</summary>
public interface IAdminMetrics
{
    Task<CoreMetrics> GetAsync(DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>Modüller gösterge paneline kendi metriklerini ekler (örn. balıkçı: av kaydı, nokta sayısı).</summary>
public interface IAdminMetricsSection
{
    string Name { get; }

    Task<IReadOnlyDictionary<string, decimal>> GetAsync(DateTimeOffset now, CancellationToken cancellationToken);
}

public sealed record DailyCount(DateOnly Date, int Count);

public sealed record NamedCount(string Key, int Count);

public sealed record CoreMetrics(
    int TotalUsers,
    int NewUsers7Days,
    int NewUsers30Days,
    int SuspendedUsers,
    IReadOnlyList<NamedCount> UsersByNiche,
    IReadOnlyList<NamedCount> UsersByRegion,
    IReadOnlyList<DailyCount> SignupsLast30Days,
    int ActiveListings,
    int ListingsLast30Days,
    decimal SalesVolume30Days,
    decimal Commission30Days,
    int OpenReports,
    int OpenDisputes,
    int AlertsLast30Days,
    int ActiveRegionSubscriptions);
