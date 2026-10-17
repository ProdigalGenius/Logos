using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SahaNet.Mobile.Core.Data;
using SahaNet.Mobile.Core.Localization;
using SahaNet.Mobile.Core.Niche;
using SahaNet.Mobile.Core.Offline;

namespace SahaNet.Mobile.Core.ViewModels;

/// <summary>Ana ekran: niş profilindeki modüller büyük ikonlu kutucuklar olarak; bekleyen kayıt sayısı.</summary>
public sealed partial class HomeViewModel(NicheProfile niche, FishingRepository repository, SyncService sync) : ObservableObject
{
    [ObservableProperty]
    private string? _syncText;

    [ObservableProperty]
    private int _activeAlertCount;

    public string AppName { get; } = niche.AppName;

    public ObservableCollection<NicheTile> Tiles { get; } = new(niche.Tiles);

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await sync.SyncAsync(cancellationToken);
        var pending = await repository.PendingCountAsync(cancellationToken);
        SyncText = pending > 0 ? Strings.Format("Sync_Pending", pending) : null;
        ActiveAlertCount = (await repository.GetAlertsAsync(cancellationToken)).Value?.Count ?? 0;
    }
}
