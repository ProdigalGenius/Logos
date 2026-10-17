using SahaNet.Mobile.Core.Niche;
using SahaNet.Mobile.Core.ViewModels;

namespace SahaNet.Mobile.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;
    private readonly NicheProfile _niche;

    public HomePage(HomeViewModel viewModel, NicheProfile niche)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _niche = niche;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshCommand.ExecuteAsync(null);
    }

    /// <summary>Acil durum: tek dokunuşla arama; konum varsa koordinat SMS ile paylaşılabilir.</summary>
    private async Task CallEmergencyAsync()
    {
        var number = _niche.EmergencyNumber ?? "112";
        var choice = await DisplayActionSheet($"🆘 Acil durum ({number})", "Vazgeç", null, $"📞 {number} ara", "📍 Konumumu SMS ile gönder");
        if (choice?.StartsWith("📞", StringComparison.Ordinal) == true && PhoneDialer.Default.IsSupported)
        {
            PhoneDialer.Default.Open(number);
        }
        else if (choice?.StartsWith("📍", StringComparison.Ordinal) == true)
        {
            var location = await Geolocation.Default.GetLastKnownLocationAsync()
                ?? await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));
            var text = location is null
                ? "Acil yardım gerekiyor. Konumum alınamadı."
                : string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"Acil yardım gerekiyor. Konumum: https://maps.google.com/?q={location.Latitude:0.00000},{location.Longitude:0.00000}");
            if (Sms.Default.IsComposeSupported)
            {
                await Sms.Default.ComposeAsync(new SmsMessage(text, Array.Empty<string>()));
            }
        }
    }

    private async void OnTileSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is NicheTile tile && sender is CollectionView list)
        {
            list.SelectedItem = null;
            if (tile.Route == "emergency")
            {
                await CallEmergencyAsync();
            }
            else
            {
                await Shell.Current.GoToAsync(tile.Route);
            }
        }
    }
}
