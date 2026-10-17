using SahaNet.Domain.Common;

namespace SahaNet.Domain.Consents;

/// <summary>
/// Kullanıcının bir metin sürümüne verdiği karar. Kayıtlar yalnızca eklenir (append-only);
/// geri çekme de yeni bir kayıttır. Böylece rıza geçmişi ispatlanabilir.
/// </summary>
public sealed class UserConsent : Entity<Guid>
{
    private UserConsent(Guid id, Guid userId, ConsentDocument document, bool granted, string source, DateTimeOffset now)
        : base(id)
    {
        UserId = userId;
        DocumentId = document.Id;
        Type = document.Type;
        Version = document.Version;
        Granted = granted;
        Source = source;
        RecordedAt = now;
    }

    // EF Core
    private UserConsent()
        : base(Guid.Empty)
    {
        Version = null!;
        Source = null!;
    }

    public Guid UserId { get; private set; }

    public Guid DocumentId { get; private set; }

    public ConsentType Type { get; private set; }

    public string Version { get; private set; }

    public bool Granted { get; private set; }

    /// <summary>Kararın alındığı kanal (mobile, web, api).</summary>
    public string Source { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public static UserConsent Record(Guid userId, ConsentDocument document, bool granted, string source, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        return new UserConsent(Guid.NewGuid(), userId, document, granted, source, now);
    }
}
