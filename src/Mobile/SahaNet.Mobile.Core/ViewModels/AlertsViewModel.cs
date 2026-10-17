using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SahaNet.Mobile.Core.Api;
using SahaNet.Mobile.Core.Data;

namespace SahaNet.Mobile.Core.ViewModels;

/// <summary>Bölgemdeki etkin uyarılar (çevrimdışıyken son bilinen liste).</summary>
public sealed partial class AlertsViewModel(FishingRepository repository) : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusText;

    public ObservableCollection<AlertDto> Alerts { get; } = [];

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var result = await repository.GetAlertsAsync(cancellationToken);
            Alerts.Clear();
            foreach (var alert in result.Value ?? [])
            {
                Alerts.Add(alert);
            }

            StatusText = result.Error ?? (Alerts.Count == 0 ? "Şu an etkin uyarı yok. ✅" : null);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
