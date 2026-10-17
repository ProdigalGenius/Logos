using SahaNet.Mobile.Core.ViewModels;

namespace SahaNet.Mobile.Views;

public partial class SpotsPage : ContentPage
{
    private readonly SpotsViewModel _viewModel;

    public SpotsPage(SpotsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.LocationProvider = GetLocationAsync;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private static async Task<(double Latitude, double Longitude)?> GetLocationAsync(CancellationToken cancellationToken)
    {
        if (await Permissions.RequestAsync<Permissions.LocationWhenInUse>() != PermissionStatus.Granted)
        {
            return null;
        }

        try
        {
            var location = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(15)), cancellationToken)
                ?? await Geolocation.Default.GetLastKnownLocationAsync();
            return location is null ? null : (location.Latitude, location.Longitude);
        }
        catch (FeatureNotEnabledException)
        {
            return null;
        }
    }
}
