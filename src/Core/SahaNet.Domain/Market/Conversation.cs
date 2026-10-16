using SahaNet.Domain.Common;

namespace SahaNet.Domain.Market;

public enum MessageKind
{
    Text = 0,
    Voice = 1,
    Photo = 2,
}

public sealed record MessageSent(Guid ConversationId, Guid MessageId, Guid SenderId) : Events.DomainEvent;

/// <summary>Birebir (ilan üzerinden veya doğrudan) sohbet. Grup sohbeti için katılımcı sayısı sınırsızdır.</summary>
public sealed class Conversation : AggregateRoot<Guid>
{
    private readonly List<ConversationParticipant> _participants = [];

    private Conversation(Guid id, Guid? listingId, DateTimeOffset now)
        : base(id)
    {
        ListingId = listingId;
        CreatedAt = now;
        LastMessageAt = now;
    }

    // EF Core
    private Conversation()
        : base(Guid.Empty)
    {
    }

    public Guid? ListingId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastMessageAt { get; private set; }

    public IReadOnlyCollection<ConversationParticipant> Participants => _participants.AsReadOnly();

    public static Conversation StartDirect(Guid listingIdOrEmpty, Guid first, Guid second, DateTimeOffset now)
    {
        if (first == second)
        {
            throw new ArgumentException("Katılımcılar farklı olmalı.", nameof(second));
        }

        var conversation = new Conversation(Guid.NewGuid(), listingIdOrEmpty == Guid.Empty ? null : listingIdOrEmpty, now);
        conversation._participants.Add(new ConversationParticipant(conversation.Id, first, now));
        conversation._participants.Add(new ConversationParticipant(conversation.Id, second, now));
        return conversation;
    }

    public bool HasParticipant(Guid userId) => _participants.Any(p => p.UserId == userId);

    public Result<ChatMessage> Send(Guid senderId, MessageKind kind, string? text, string? mediaUrl, DateTimeOffset now)
    {
        if (!HasParticipant(senderId))
        {
            return Result.Failure<ChatMessage>(MarketErrors.ConversationNotFound);
        }

        var message = new ChatMessage(Guid.NewGuid(), Id, senderId, kind, text?.Trim(), mediaUrl, now);
        LastMessageAt = now;
        MarkRead(senderId, now);
        Raise(new MessageSent(Id, message.Id, senderId));
        return Result.Success(message);
    }

    public void MarkRead(Guid userId, DateTimeOffset now) =>
        _participants.FirstOrDefault(p => p.UserId == userId)?.MarkRead(now);
}

public sealed class ConversationParticipant
{
    internal ConversationParticipant(Guid conversationId, Guid userId, DateTimeOffset now)
    {
        ConversationId = conversationId;
        UserId = userId;
        JoinedAt = now;
        LastReadAt = now;
    }

    // EF Core
    private ConversationParticipant()
    {
    }

    public Guid ConversationId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    public DateTimeOffset LastReadAt { get; private set; }

    internal void MarkRead(DateTimeOffset now)
    {
        if (now > LastReadAt)
        {
            LastReadAt = now;
        }
    }
}

/// <summary>Sohbet mesajı. Ayrı tabloda tutulur (sohbet aggregate'ini büyütmemek için).</summary>
public sealed class ChatMessage : Entity<Guid>
{
    public const int TextMaxLength = 2000;

    internal ChatMessage(Guid id, Guid conversationId, Guid senderId, MessageKind kind, string? text, string? mediaUrl, DateTimeOffset now)
        : base(id)
    {
        ConversationId = conversationId;
        SenderId = senderId;
        Kind = kind;
        Text = text;
        MediaUrl = mediaUrl;
        SentAt = now;
    }

    // EF Core
    private ChatMessage()
        : base(Guid.Empty)
    {
    }

    public Guid ConversationId { get; private set; }

    public Guid SenderId { get; private set; }

    public MessageKind Kind { get; private set; }

    public string? Text { get; private set; }

    public string? MediaUrl { get; private set; }

    public DateTimeOffset SentAt { get; private set; }
}
