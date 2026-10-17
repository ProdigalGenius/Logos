using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SahaNet.Mobile.Core.Api;
using SahaNet.Mobile.Core.Localization;
using SahaNet.Mobile.Core.Niche;

namespace SahaNet.Mobile.Core.ViewModels;

public enum OnboardingStep
{
    Phone = 0,
    Code = 1,
    Profile = 2,
    Done = 3,
}

public sealed partial class ConsentItem(ConsentDocumentDto document) : ObservableObject
{
    public ConsentDocumentDto Document { get; } = document;

    [ObservableProperty]
    private bool _accepted;
}

/// <summary>Kurulum en fazla 3 adım: telefon → kod → rızalar + bölge (niş, build profilinden gelir).</summary>
public sealed partial class OnboardingViewModel(ISahaNetApi api, NicheProfile niche) : ObservableObject
{
    [ObservableProperty]
    private OnboardingStep _step;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCodeCommand))]
    private string _phone = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerifyCommand))]
    private string _code = string.Empty;

    [ObservableProperty]
    private string? _displayName;

    [ObservableProperty]
    private RegionDto? _selectedRegion;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<ConsentItem> Consents { get; } = [];

    public ObservableCollection<RegionDto> Regions { get; } = [];

    public event EventHandler? Completed;

    private bool CanSendCode() => Phone.Count(char.IsAsciiDigit) >= 10;

    private bool CanVerify() => Code.Length >= 4;

    [RelayCommand(CanExecute = nameof(CanSendCode))]
    private Task SendCodeAsync(CancellationToken cancellationToken) => RunAsync(async () =>
    {
        var result = await api.RequestOtpAsync(Phone, cancellationToken);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return;
        }

        Step = OnboardingStep.Code;
    });

    [RelayCommand(CanExecute = nameof(CanVerify))]
    private Task VerifyAsync(CancellationToken cancellationToken) => RunAsync(async () =>
    {
        var result = await api.VerifyOtpAsync(Phone, Code, cancellationToken);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return;
        }

        if (!result.Value!.RequiresOnboarding)
        {
            Finish();
            return;
        }

        var documents = await api.GetConsentDocumentsAsync(cancellationToken);
        var regions = await api.GetRegionsAsync(cancellationToken);
        Consents.Clear();
        foreach (var document in documents.Value ?? [])
        {
            Consents.Add(new ConsentItem(document));
        }

        Regions.Clear();
        foreach (var region in regions.Value ?? [])
        {
            Regions.Add(region);
        }

        SelectedRegion = Regions.FirstOrDefault(r => r.Code == niche.DefaultRegionCode);
        Step = OnboardingStep.Profile;
    });

    [RelayCommand]
    private Task CompleteAsync(CancellationToken cancellationToken) => RunAsync(async () =>
    {
        if (Consents.Any(c => c.Document.IsRequired && !c.Accepted))
        {
            ErrorMessage = Strings.Get("Onboarding_ConsentsRequired");
            return;
        }

        if (SelectedRegion is null)
        {
            ErrorMessage = Strings.Get("Onboarding_SelectRegion");
            return;
        }

        // Konum paylaşımı gibi isteğe bağlı rızalar da açıkça kaydedilir (verilmedi = false).
        var decisions = Consents.Select(c => new ConsentDecisionDto(c.Document.Id, c.Accepted)).ToList();
        var consent = await api.RecordConsentsAsync(decisions, cancellationToken);
        if (!consent.IsSuccess)
        {
            ErrorMessage = consent.ErrorMessage;
            return;
        }

        var profile = await api.CompleteOnboardingAsync(niche.Key, SelectedRegion.Code, DisplayName, cancellationToken);
        if (!profile.IsSuccess)
        {
            ErrorMessage = profile.ErrorMessage;
            return;
        }

        Finish();
    });

    private void Finish()
    {
        Step = OnboardingStep.Done;
        Completed?.Invoke(this, EventArgs.Empty);
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await action();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
