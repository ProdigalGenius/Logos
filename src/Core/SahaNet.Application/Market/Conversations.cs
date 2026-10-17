using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Domain.Common;
using SahaNet.Domain.Identity;
using SahaNet.Domain.Market;
using SahaNet.Application.Moderation;

namespace SahaNet.Application.Market;

public sealed record ParticipantResponse(Guid UserId, string? DisplayName);

public sealed record ConversationResponse(
    Guid Id,
    Guid? ListingId,
    IReadOnlyList<ParticipantResponse> Participants,
    MessageResponse? LastMessage,
    int UnreadCount,
    DateTimeOffset LastMessageAt);

public static class MarketChatErrors
{
    public static readonly Error MessageRateLimited = new("chat.rate_limited", "Çok hızlı mesaj gönderiyorsunuz, biraz bekleyin.", ErrorType.TooManyRequests);
    public static readonly Error EmptyMessage = new("chat.empty", "Mesaj boş olamaz.", ErrorType.Validation);
    public static readonly Error CannotChatWithSelf = new("chat.self", "Kendinizle sohbet başlatamazsınız.", ErrorType.Validation);
}

internal static class ChatMapping
{
    public static MessageResponse ToResponse(this ChatMessage m) =>
        new(m.Id, m.ConversationId, m.SenderId, m.Kind.ToString(), m.Text, m.MediaUrl, m.SentAt);
}

// ---- Sohbet başlat (ilan sahibiyle veya doğrudan) ----

public sealed record StartConversationCommand(Guid? ListingId, Guid? UserId) : IRequest<Result<ConversationResponse>>;

internal sealed class StartConversationCommandValidator : AbstractValidator<StartConversationCommand>
{
    public StartConversationCommandValidator() =>
        RuleFor(x => x).Must(x => x.ListingId is not null || x.UserId is not null).WithMessage("İlan veya kullanıcı belirtilmeli.");
}

internal sealed class StartConversationCommandHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    IListingRepository listings,
    IConversationRepository conversations,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<StartConversationCommand, Result<ConversationResponse>>
{
    public async Task<Result<ConversationResponse>> Handle(StartConversationCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<ConversationResponse>(IdentityErrors.Unauthenticated);
        }

        Guid otherId;
        if (request.ListingId is { } listingId)
        {
            var listing = await listings.GetByIdAsync(listingId, cancellationToken);
            if (listing is null)
            {
                return Result.Failure<ConversationResponse>(MarketErrors.ListingNotFound);
            }

            // Satıcı kendi ilanında belirli bir alıcıyla konuşabilir; alıcı satıcıyla.
            otherId = listing.SellerId == userId && request.UserId is { } buyer ? buyer : listing.SellerId;
        }
        else
        {
            otherId = request.UserId!.Value;
        }

        if (otherId == userId)
        {
            return Result.Failure<ConversationResponse>(MarketChatErrors.CannotChatWithSelf);
        }

        var other = await users.GetByIdAsync(otherId, cancellationToken);
        if (other is null || other.Status != UserStatus.Active)
        {
            return Result.Failure<ConversationResponse>(IdentityErrors.UserNotFound);
        }

        var conversation = await conversations.FindDirectAsync(request.ListingId, userId, otherId, cancellationToken);
        if (conversation is null)
        {
            conversation = Conversation.StartDirect(request.ListingId ?? Guid.Empty, userId, otherId, clock.UtcNow);
            conversations.Add(conversation);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var me = await users.GetByIdAsync(userId, cancellationToken);
        return Result.Success(new ConversationResponse(
            conversation.Id,
            conversation.ListingId,
            [new ParticipantResponse(userId, me?.DisplayName), new ParticipantResponse(otherId, other.DisplayName)],
            null,
            0,
            conversation.LastMessageAt));
    }
}

// ---- Sohbetlerim ----

public sealed record GetMyConversationsQuery : IRequest<Result<IReadOnlyList<ConversationResponse>>>;

internal sealed class GetMyConversationsQueryHandler(ICurrentUser currentUser, IConversationRepository conversations, IUserRepository users)
    : IRequestHandler<GetMyConversationsQuery, Result<IReadOnlyList<ConversationResponse>>>
{
    public async Task<Result<IReadOnlyList<ConversationResponse>>> Handle(GetMyConversationsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<IReadOnlyList<ConversationResponse>>(IdentityErrors.Unauthenticated);
        }

        var summaries = await conversations.GetForUserAsync(userId, cancellationToken);
        var participantIds = summaries.SelectMany(s => s.Conversation.Participants).Select(p => p.UserId).Distinct().ToList();
        var names = (await users.GetByIdsAsync(participantIds, cancellationToken)).ToDictionary(u => u.Id, u => u.DisplayName);

        return Result.Success<IReadOnlyList<ConversationResponse>>(summaries
            .Select(s => new ConversationResponse(
                s.Conversation.Id,
                s.Conversation.ListingId,
                s.Conversation.Participants.Select(p => new ParticipantResponse(p.UserId, names.GetValueOrDefault(p.UserId))).ToList(),
                s.LastMessage?.ToResponse(),
                s.UnreadCount,
                s.Conversation.LastMessageAt))
            .OrderByDescending(c => c.LastMessageAt)
            .ToList());
    }
}

// ---- Mesajlar ----

public sealed record GetMessagesQuery(Guid ConversationId, DateTimeOffset? Before, int Limit = 50) : IRequest<Result<IReadOnlyList<MessageResponse>>>;

internal sealed class GetMessagesQueryValidator : AbstractValidator<GetMessagesQuery>
{
    public GetMessagesQueryValidator() => RuleFor(x => x.Limit).InclusiveBetween(1, 200);
}

internal sealed class GetMessagesQueryHandler(ICurrentUser currentUser, IConversationRepository conversations, IUnitOfWork unitOfWork, IDateTimeProvider clock)
    : IRequestHandler<GetMessagesQuery, Result<IReadOnlyList<MessageResponse>>>
{
    public async Task<Result<IReadOnlyList<MessageResponse>>> Handle(GetMessagesQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<IReadOnlyList<MessageResponse>>(IdentityErrors.Unauthenticated);
        }

        var conversation = await conversations.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation is null || !conversation.HasParticipant(userId))
        {
            return Result.Failure<IReadOnlyList<MessageResponse>>(MarketErrors.ConversationNotFound);
        }

        var messages = await conversations.GetMessagesAsync(conversation.Id, request.Before, request.Limit, cancellationToken);
        if (request.Before is null)
        {
            conversation.MarkRead(userId, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success<IReadOnlyList<MessageResponse>>(messages.Select(m => m.ToResponse()).ToList());
    }
}

public sealed record SendMessageCommand(Guid ConversationId, string? Text, string? Kind, string? MediaUrl) : IRequest<Result<MessageResponse>>;

internal sealed class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(x => x.Text).MaximumLength(ChatMessage.TextMaxLength);
        RuleFor(x => x.Kind).Must(k => k is null || Enum.TryParse<MessageKind>(k, true, out _));
        RuleFor(x => x.MediaUrl).MaximumLength(1024).Must(u => u is null || Uri.TryCreate(u, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            .WithMessage("Medya adresi https olmalı.");
    }
}

internal sealed class SendMessageCommandHandler(
    ICurrentUser currentUser,
    IConversationRepository conversations,
    IUserRepository users,
    IChatRealtime realtime,
    IUserNotifier notifier,
    SahaNet.Application.Moderation.ISpamFilter spamFilter,
    SahaNet.Application.Moderation.IModerationService moderation,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    IOptions<MarketOptions> options)
    : IRequestHandler<SendMessageCommand, Result<MessageResponse>>
{
    public async Task<Result<MessageResponse>> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<MessageResponse>(IdentityErrors.Unauthenticated);
        }

        var kind = request.Kind is null ? MessageKind.Text : Enum.Parse<MessageKind>(request.Kind, true);
        if ((kind == MessageKind.Text && string.IsNullOrWhiteSpace(request.Text)) || (kind != MessageKind.Text && request.MediaUrl is null))
        {
            return Result.Failure<MessageResponse>(MarketChatErrors.EmptyMessage);
        }

        var conversation = await conversations.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation is null || !conversation.HasParticipant(userId))
        {
            return Result.Failure<MessageResponse>(MarketErrors.ConversationNotFound);
        }

        var sender = await users.GetByIdAsync(userId, cancellationToken);
        if (sender is null || sender.Status != UserStatus.Active)
        {
            return Result.Failure<MessageResponse>(IdentityErrors.AccountSuspended);
        }

        var spam = spamFilter.Evaluate(SpamContent.Message, request.Text, []);
        if (spam.Verdict == SpamVerdict.Block)
        {
            return Result.Failure<MessageResponse>(SahaNet.Domain.Moderation.ModerationErrors.SpamBlocked);
        }

        var now = clock.UtcNow;
        if (await conversations.CountRecentMessagesAsync(userId, now.AddMinutes(-1), cancellationToken) >= options.Value.MaxMessagesPerMinute)
        {
            return Result.Failure<MessageResponse>(MarketChatErrors.MessageRateLimited);
        }

        var message = conversation.Send(userId, kind, request.Text, request.MediaUrl, now);
        if (message.IsFailure)
        {
            return Result.Failure<MessageResponse>(message.Error);
        }

        conversations.AddMessage(message.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = message.Value.ToResponse();
        var recipients = conversation.Participants.Select(p => p.UserId).Where(id => id != userId).ToList();
        await realtime.MessageSentAsync(conversation.Participants.Select(p => p.UserId).ToList(), response, cancellationToken);

        if (spam.Verdict == SpamVerdict.Review)
        {
            await moderation.FlagAsync(SahaNet.Domain.Moderation.ReportTargetType.Message, message.Value.Id, userId, spam, cancellationToken);
        }

        var preview = kind switch
        {
            MessageKind.Voice => "🎤 Sesli mesaj",
            MessageKind.Photo => "📷 Fotoğraf",
            _ => request.Text!.Length > 80 ? request.Text[..80] + "…" : request.Text,
        };
        foreach (var recipient in recipients)
        {
            await notifier.NotifyAsync(
                recipient,
                sender?.DisplayName ?? "Yeni mesaj",
                preview,
                new Dictionary<string, string> { ["type"] = "chat", ["conversationId"] = conversation.Id.ToString() },
                cancellationToken);
        }

        return Result.Success(response);
    }
}
