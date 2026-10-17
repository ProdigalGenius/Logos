using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SahaNet.Mobile.Core.Api;

namespace SahaNet.Mobile.Core.ViewModels;

public sealed record ConversationItem(Guid Id, string Title, string? Preview, int UnreadCount, DateTimeOffset LastMessageAt);

/// <summary>Sohbetlerim.</summary>
public sealed partial class ConversationsViewModel(ICommunityApi api, ITokenStore tokens) : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusText;

    public ObservableCollection<ConversationItem> Conversations { get; } = [];

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var me = JwtSubject.Read((await tokens.GetAsync())?.AccessToken);
            var result = await api.GetConversationsAsync(cancellationToken);
            Conversations.Clear();
            foreach (var c in result.Value ?? [])
            {
                var other = c.Participants.FirstOrDefault(p => p.UserId != me);
                Conversations.Add(new ConversationItem(c.Id, other?.DisplayName ?? "Kullanıcı", c.LastMessage?.Text, c.UnreadCount, c.LastMessageAt));
            }

            StatusText = result.ErrorMessage ?? (Conversations.Count == 0 ? "Henüz sohbetiniz yok. Pazardaki bir ilana yazarak başlayın." : null);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed record ChatLine(string? Text, bool IsMine, DateTimeOffset SentAt);

/// <summary>Tek sohbet: mesajlar (sayfa açıkken periyodik yenilenir) ve gönderme.</summary>
public sealed partial class ChatViewModel(ICommunityApi api, ITokenStore tokens) : ObservableObject
{
    [ObservableProperty]
    private string? _draft;

    [ObservableProperty]
    private string? _statusText;

    public Guid ConversationId { get; set; }

    public ObservableCollection<ChatLine> Messages { get; } = [];

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var me = JwtSubject.Read((await tokens.GetAsync())?.AccessToken);
        var result = await api.GetMessagesAsync(ConversationId, cancellationToken);
        if (result.Value is not { } messages)
        {
            StatusText = result.ErrorMessage;
            return;
        }

        StatusText = null;
        var ordered = messages.OrderBy(m => m.SentAt).Select(m => new ChatLine(m.Text, m.SenderId == me, m.SentAt)).ToList();
        if (ordered.Count == Messages.Count && (ordered.Count == 0 || ordered[^1] == Messages[^1]))
        {
            return;
        }

        Messages.Clear();
        foreach (var line in ordered)
        {
            Messages.Add(line);
        }
    }

    [RelayCommand]
    private async Task SendAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Draft))
        {
            return;
        }

        var result = await api.SendMessageAsync(ConversationId, Draft.Trim(), cancellationToken);
        if (!result.IsSuccess)
        {
            StatusText = result.ErrorMessage;
            return;
        }

        Draft = null;
        await LoadAsync(cancellationToken);
    }
}
