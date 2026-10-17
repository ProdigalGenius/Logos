using SahaNet.Domain.Common;

namespace SahaNet.Domain.Geo;

/// <summary>Abone olunan konular. Kritik uyarılar konudan bağımsız olarak her aboneye gider.</summary>
[Flags]
public enum AlertTopics
{
    None = 0,

    /// <summary>Hava/deniz uyarıları (uyarı seviyesi).</summary>
    Weather = 1,

    /// <summary>Pazar ve ilan duyuruları (Faz 4).</summary>
    Market = 2,

    /// <summary>Topluluk ihbarları (Faz 3+).</summary>
    Community = 4,

    All = Weather | Market | Community,
}

public sealed class RegionSubscription : Entity<Guid>
{
    private RegionSubscription(Guid id, Guid userId, Guid regionId, AlertTopics topics, DateTimeOffset now)
        : base(id)
    {
        UserId = userId;
        RegionId = regionId;
        Topics = topics;
        CreatedAt = now;
    }

    // EF Core
    private RegionSubscription()
        : base(Guid.Empty)
    {
    }

    public Guid UserId { get; private set; }

    public Guid RegionId { get; private set; }

    public AlertTopics Topics { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static RegionSubscription Create(Guid userId, Guid regionId, AlertTopics topics, DateTimeOffset now) =>
        new(Guid.NewGuid(), userId, regionId, topics, now);

    public void ChangeTopics(AlertTopics topics) => Topics = topics;

    public bool Wants(AlertTopics topic) => (Topics & topic) == topic;
}
