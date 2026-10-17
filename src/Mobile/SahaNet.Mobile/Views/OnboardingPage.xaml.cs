using System.ComponentModel;
using SahaNet.Mobile.Core.ViewModels;
using SahaNet.Mobile.Services;

namespace SahaNet.Mobile.Views;

public partial class OnboardingPage : ContentPage
{
    private readonly OnboardingViewModel _viewModel;
    private readonly IVoiceInput _voice;

    public OnboardingPage(OnboardingViewModel viewModel, IVoiceInput voice)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _voice = voice;
        _viewModel.PropertyChanged += OnViewModelChanged;
        _viewModel.Completed += (_, _) => ((App)Application.Current!).ShowShell();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OnboardingViewModel.Step))
        {
            return;
        }

        PhoneStep.IsVisible = _viewModel.Step == OnboardingStep.Phone;
        CodeStep.IsVisible = _viewModel.Step == OnboardingStep.Code;
        ProfileStep.IsVisible = _viewModel.Step == OnboardingStep.Profile;
    }

    private async void OnPhoneMic(object? sender, EventArgs e)
    {
        var text = await _voice.ListenAsync(CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(text))
        {
            _viewModel.Phone = new string(text.Where(char.IsAsciiDigit).ToArray());
        }
    }
}
