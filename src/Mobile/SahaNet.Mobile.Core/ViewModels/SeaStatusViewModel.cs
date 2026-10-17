using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SahaNet.Mobile.Core.Api;
using SahaNet.Mobile.Core.Data;
using SahaNet.Mobile.Core.Localization;
using SahaNet.Mobile.Core.Niche;

namespace SahaNet.Mobile.Core.ViewModels;

/// <summary>Deniz durumu: tek bakışta renk + büyük ikon + kısa cümle (okuma gerektirmeyen tasarım).</summary>
public sealed partial class SeaStatusViewModel(FishingRepository repository, NicheProfile niche) : ObservableObject
{
    [ObservableProperty]
    private string _portCode = niche.DefaultPortCode ?? string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VerdictText), nameof(VerdictColor), nameof(VerdictIcon))]
    private SeaStatusDto? _status;

    [ObservableProperty]
    private string? _staleText;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public string VerdictText => Status?.Now switch
    {
        "NoGo" => Strings.Get("Sea_NoGo"),
        "Caution" => Strings.Get("Sea_Caution"),
        "Go" => Strings.Get("Sea_Go"),
        _ => string.Empty,
    };

    /// <summary>Renk körlüğüne karşı renk tek başına kullanılmaz; ikon ve metin de değişir.</summary>
    public string VerdictColor => Status?.Now switch
    {
        "NoGo" => "#C62828",
        "Caution" => "#F9A825",
        _ => "#2E7D32",
    };

    public string VerdictIcon => Status?.Now switch
    {
        "NoGo" => "⛔",
        "Caution" => "⚠️",
        _ => "✅",
    };

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(PortCode))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await repository.GetSeaStatusAsync(PortCode, cancellationToken);
            Status = result.Value;
            StaleText = result.FromCache && result.AsOf is { } asOf ? Strings.Format("Sea_Stale", asOf.ToLocalTime()) : null;
            ErrorMessage = result.HasValue ? null : result.Error;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
