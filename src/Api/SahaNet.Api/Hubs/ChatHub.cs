using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SahaNet.Api.Infrastructure;
using SahaNet.Application.Market;

namespace SahaNet.Api.Hubs;

/// <summary>
/// Gerçek zamanlı sohbet. Sunucu → istemci: "messageReceived". İstemci mesajı hub üzerinden veya
/// REST ile gönderebilir; her iki yol da aynı komutu kullanır (doğrulama, hız sınırı, kalıcılık).
/// </summary>
[Authorize]
internal sealed class ChatHub(ISender sender, HttpCurrentUser currentUser) : Hub
{
    public const string Path = "/hubs/chat";
    public const string MessageReceived = "messageReceived";

    public async Task<MessageResponse> SendMessage(Guid conversationId, string text)
    {
        currentUser.Override = Guid.TryParse(Context.UserIdentifier, out var id) ? id : null;
        var result = await sender.Send(new SendMessageCommand(conversationId, text, null, null), Context.ConnectionAborted);
        return result.IsSuccess ? result.Value : throw new HubException(result.Error.Code);
    }
}

/// <summary>JWT "sub" talebini SignalR kullanıcı kimliği olarak kullanır.</summary>
internal sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User?.FindFirst("sub")?.Value;
}

internal sealed class SignalRChatRealtime(IHubContext<ChatHub> hub) : IChatRealtime
{
    public Task MessageSentAsync(IReadOnlyCollection<Guid> recipientIds, MessageResponse message, CancellationToken cancellationToken) =>
        hub.Clients.Users(recipientIds.Select(id => id.ToString()).ToList()).SendAsync(ChatHub.MessageReceived, message, cancellationToken);
}
