using SahaNet.Mobile.Core.ViewModels;

namespace SahaNet.Mobile.Views;

public partial class PickupsPage : ContentPage
{
    private readonly PickupsViewModel _viewModel;

    public PickupsPage(PickupsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
