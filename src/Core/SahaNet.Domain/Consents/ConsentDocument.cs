using SahaNet.Domain.Common;

namespace SahaNet.Domain.Consents;

/// <summary>Versiyonlu aydınlatma / rıza metni. Yayınlanan sürüm değiştirilmez, yenisi eklenir.</summary>
public sealed class ConsentDocument : Entity<Guid>
{
    public ConsentDocument(Guid id, ConsentType type, string version, string title, string content, bool isRequired, DateTimeOffset publishedAt)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        Type = type;
        Version = version;
        Title = title;
        Content = content;
        IsRequired = isRequired;
        PublishedAt = publishedAt;
        IsActive = true;
    }

    // EF Core
    private ConsentDocument()
        : base(Guid.Empty)
    {
        Version = null!;
        Title = null!;
        Content = null!;
    }

    public ConsentType Type { get; private set; }

    public string Version { get; private set; }

    public string Title { get; private set; }

    public string Content { get; private set; }

    /// <summary>Onboarding için kabul edilmesi zorunlu mu.</summary>
    public bool IsRequired { get; private set; }

    public DateTimeOffset PublishedAt { get; private set; }

    public bool IsActive { get; private set; }

    public void Retire() => IsActive = false;
}
