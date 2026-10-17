using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SahaNet.Mobile.Core.Api;
using SahaNet.Mobile.Core.Data;
using SahaNet.Mobile.Core.Localization;

namespace SahaNet.Mobile.Core.ViewModels;

/// <summary>Av günlüğü: tür seç, kilo yaz (veya söyle), kaydet. İnternet yoksa kuyruğa alınır.</summary>
public sealed partial class CatchLogViewModel(FishingRepository repository, TimeProvider clock) : ObservableObject
{
    [ObservableProperty]
    private SpeciesDto? _selectedSpecies;

    [ObservableProperty]
    private string _weightText = string.Empty;

    [ObservableProperty]
    private string _lengthText = string.Empty;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private int _pendingCount;

    public ObservableCollection<SpeciesDto> Species { get; } = [];

    public ObservableCollection<CatchItem> Catches { get; } = [];

    public ObservableCollection<string> Warnings { get; } = [];

    /// <summary>"2,5", "2.5" ve "2,5 kg" kabul edilir (sesli girişten gelen metin dahil).</summary>
    public static double? ParseNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var cleaned = new string(text.Trim().Replace(',', '.').TakeWhile(c => char.IsAsciiDigit(c) || c == '.').ToArray());
        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : null;
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var species = await repository.GetSpeciesAsync(cancellationToken);
        Species.Clear();
        foreach (var s in species.Value ?? [])
        {
            Species.Add(s);
        }

        await RefreshCatchesAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        Warnings.Clear();
        if (SelectedSpecies is null)
        {
            Message = Strings.Get("Catch_SelectSpecies");
            return;
        }

        if (ParseNumber(WeightText) is not { } weight)
        {
            Message = Strings.Get("Catch_InvalidWeight");
            return;
        }

        IsBusy = true;
        try
        {
            var request = new RecordCatchRequest(clock.GetUtcNow(), SelectedSpecies.Code, weight, null, ParseNumber(LengthText), null, null, Notes, null);
            var (_, warnings, queued) = await repository.RecordCatchAsync(request, cancellationToken);
            foreach (var warning in warnings)
            {
                Warnings.Add(warning);
            }

            Message = Strings.Get(queued ? "Catch_Queued" : "Catch_Saved");
            WeightText = string.Empty;
            LengthText = string.Empty;
            Notes = null;
            await RefreshCatchesAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshCatchesAsync(CancellationToken cancellationToken)
    {
        var catches = await repository.GetCatchesAsync(cancellationToken);
        Catches.Clear();
        foreach (var item in catches.Value ?? [])
        {
            Catches.Add(item);
        }

        PendingCount = await repository.PendingCountAsync(cancellationToken);
    }
}
