using SahaNet.Mobile.Core.ViewModels;

namespace SahaNet.Mobile.Views;

public partial class MarketPage : ContentPage
{
    private readonly MarketViewModel _viewModel;

    public MarketPage(MarketViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.ConversationOpened += async (_, id) => await Shell.Current.GoToAsync($"chat?id={id}");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
