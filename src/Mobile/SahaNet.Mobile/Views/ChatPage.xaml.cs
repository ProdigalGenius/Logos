using SahaNet.Mobile.Core.ViewModels;

namespace SahaNet.Mobile.Views;

/// <summary>Sohbet ekranı; açıkken birkaç saniyede bir yenilenir (push bildirimi gelene kadar basit ve dayanıklı).</summary>
[QueryProperty(nameof(ConversationId), "id")]
public partial class ChatPage : ContentPage
{
    private readonly ChatViewModel _viewModel;
    private IDispatcherTimer? _timer;

    public ChatPage(ChatViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.Messages.CollectionChanged += (_, _) =>
        {
            if (_viewModel.Messages.Count > 0)
            {
                MessageList.ScrollTo(_viewModel.Messages.Count - 1, animate: false);
            }
        };
    }

    public string ConversationId
    {
        set => _viewModel.ConversationId = Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(5);
        _timer.Tick += async (_, _) =>
        {
            if (!_viewModel.LoadCommand.IsRunning)
            {
                await _viewModel.LoadCommand.ExecuteAsync(null);
            }
        };
        _timer.Start();
    }

    protected override void OnDisappearing()
    {
        _timer?.Stop();
        _timer = null;
        base.OnDisappearing();
    }
}
