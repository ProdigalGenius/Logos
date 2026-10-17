using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SahaNet.Mobile.Core.Api;
using SahaNet.Mobile.Core.Niche;

namespace SahaNet.Mobile.Core.ViewModels;

/// <summary>Günlük hurda fiyatları (bildirimlerin medyanı, TL/kg).</summary>
public sealed partial class ScrapPricesViewModel(ICommunityApi api, NicheProfile niche) : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusText;

    [ObservableProperty]
    private string _provinceCode = niche.DefaultRegionCode;

    public ObservableCollection<ScrapPriceDto> Prices { get; } = [];

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var result = await api.GetScrapPricesAsync(ProvinceCode, cancellationToken);
            Prices.Clear();
            foreach (var price in (result.Value ?? []).OrderByDescending(p => p.Date).ThenBy(p => p.MaterialName, StringComparer.CurrentCulture))
            {
                Prices.Add(price);
            }

            StatusText = result.ErrorMessage ?? (Prices.Count == 0 ? "Bu ilde son günlerde fiyat bildirimi yok." : null);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed partial class MaterialChoice(MaterialDto material) : ObservableObject
{
    public MaterialDto Material { get; } = material;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>"Evimde hurda var" talebi oluştur; taleplerimi ve (hurdacıysam) ilimdeki açık talepleri gör.</summary>
public sealed partial class PickupsViewModel(ICommunityApi api, NicheProfile niche) : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusText;

    [ObservableProperty]
    private string _provinceCode = niche.DefaultRegionCode;

    [ObservableProperty]
    private string? _district;

    [ObservableProperty]
    private string? _address;

    [ObservableProperty]
    private string? _estimatedKg;

    public ObservableCollection<MaterialChoice> Materials { get; } = [];

    public ObservableCollection<PickupDto> Mine { get; } = [];

    public ObservableCollection<PickupDto> Open { get; } = [];

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            if (Materials.Count == 0 && (await api.GetMaterialsAsync(cancellationToken)).Value is { } materials)
            {
                foreach (var material in materials)
                {
                    Materials.Add(new MaterialChoice(material));
                }
            }

            var mine = await api.GetMyPickupsAsync(cancellationToken);
            Replace(Mine, mine.Value);
            Replace(Open, (await api.GetOpenPickupsAsync(ProvinceCode, cancellationToken)).Value?.Where(p => !p.IsMine).ToList());
            StatusText = mine.ErrorMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        var selected = Materials.Where(m => m.IsSelected).Select(m => m.Material.Code).ToList();
        if (string.IsNullOrWhiteSpace(District) || string.IsNullOrWhiteSpace(Address) || selected.Count == 0
            || !MarketViewModel.TryParseDecimal(EstimatedKg, out var kg))
        {
            StatusText = "İlçe, adres, en az bir malzeme ve tahmini kilo gerekli.";
            return;
        }

        var result = await api.CreatePickupAsync(new CreatePickupRequest(ProvinceCode, District.Trim(), Address.Trim(), selected, kg, null), cancellationToken);
        if (!result.IsSuccess)
        {
            StatusText = result.ErrorMessage;
            return;
        }

        District = Address = EstimatedKg = null;
        foreach (var material in Materials)
        {
            material.IsSelected = false;
        }

        await LoadAsync(cancellationToken);
        StatusText = "Talebiniz alındı. Adresiniz yalnızca talebi üstlenen hurdacıya görünür. 🚚";
    }

    [RelayCommand]
    private Task ClaimAsync(PickupDto pickup, CancellationToken cancellationToken) => ActAsync(pickup, "claim", cancellationToken);

    [RelayCommand]
    private Task CompleteAsync(PickupDto pickup, CancellationToken cancellationToken) => ActAsync(pickup, "complete", cancellationToken);

    [RelayCommand]
    private Task CancelAsync(PickupDto pickup, CancellationToken cancellationToken) => ActAsync(pickup, "cancel", cancellationToken);

    private async Task ActAsync(PickupDto pickup, string action, CancellationToken cancellationToken)
    {
        var result = await api.PickupActionAsync(pickup.Id, action, cancellationToken);
        await LoadAsync(cancellationToken);
        StatusText = result.ErrorMessage;
    }

    private static void Replace(ObservableCollection<PickupDto> target, IReadOnlyList<PickupDto>? items)
    {
        target.Clear();
        foreach (var item in items ?? [])
        {
            target.Add(item);
        }
    }
}
