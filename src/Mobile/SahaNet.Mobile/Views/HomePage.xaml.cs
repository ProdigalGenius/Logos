using SahaNet.Mobile.Core.Niche;
using SahaNet.Mobile.Core.ViewModels;

namespace SahaNet.Mobile.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;

    public HomePage(HomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshCommand.ExecuteAsync(null);
    }

    private async void OnTileSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is NicheTile tile && sender is CollectionView list)
        {
            list.SelectedItem = null;
            if (tile.Route is "sea" or "catches")
            {
                await Shell.Current.GoToAsync(tile.Route);
            }
            else
            {
                await DisplayAlert(tile.Title, "Bu ekran bir sonraki sürümde.", "Tamam");
            }
        }
    }
}
