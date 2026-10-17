using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SahaNet.Mobile.Core.Api;
using SahaNet.Mobile.Core.Data;

namespace SahaNet.Mobile.Core.ViewModels;

/// <summary>Av noktalarım (gizli; yalnızca seçtiğim kişilerle paylaşılır). Yeni nokta bulunduğum konumdan eklenir.</summary>
public sealed partial class SpotsViewModel(FishingRepository repository) : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _newName;

    [ObservableProperty]
    private string? _statusText;

    public ObservableCollection<SpotItem> Spots { get; } = [];

    /// <summary>Platform tarafı konumu verir (izin reddedilirse null).</summary>
    public Func<CancellationToken, Task<(double Latitude, double Longitude)?>>? LocationProvider { get; set; }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var result = await repository.GetSpotsAsync(cancellationToken);
            Spots.Clear();
            foreach (var spot in result.Value ?? [])
            {
                Spots.Add(spot);
            }

            StatusText = result.Error ?? (Spots.Count == 0 ? "Henüz kayıtlı noktanız yok." : null);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddHereAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(NewName))
        {
            StatusText = "Noktaya bir ad verin.";
            return;
        }

        var location = LocationProvider is null ? null : await LocationProvider(cancellationToken);
        if (location is not { } here)
        {
            StatusText = "Konum alınamadı. Konum iznini ve GPS'i kontrol edin.";
            return;
        }

        var (_, queued) = await repository.SaveSpotAsync(null, new SaveSpotRequest(NewName.Trim(), here.Latitude, here.Longitude, null, null, null, null), cancellationToken);
        NewName = null;
        await LoadAsync(cancellationToken);
        StatusText = queued ? "Kaydedildi; bağlantı gelince gönderilecek." : "Nokta kaydedildi. 📍";
    }
}
