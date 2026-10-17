using System.ComponentModel;
using SahaNet.Mobile.Core.Api;
using SahaNet.Mobile.Core.ViewModels;
using SahaNet.Mobile.Services;

namespace SahaNet.Mobile.Views;

public partial class OnboardingPage : ContentPage
{
    private readonly OnboardingViewModel _viewModel;
    private readonly IVoiceInput _voice;
    /// <summary>Geliştirme sunucusunda <c>Identity:AdminPhones</c> içindeki ana yönetici numarası.</summary>
    private const string AdminPhone = "05550000000";

    private readonly ApiEndpoint _endpoint;
    private readonly ICommunityApi _community;

    public OnboardingPage(OnboardingViewModel viewModel, IVoiceInput voice, ApiEndpoint endpoint, ICommunityApi community)
    {
        InitializeComponent();
        _endpoint = endpoint;
        _community = community;
        ServerEntry.Text = endpoint.BaseUrl.ToString();
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

    private async void OnAdminLogin(object? sender, EventArgs e) => await DevLoginAsync(AdminPhone);

    private async void OnPasswordlessLogin(object? sender, EventArgs e) => await DevLoginAsync(_viewModel.Phone);

    private async Task DevLoginAsync(string phone)
    {
        OnServerUnfocused(this, new FocusEventArgs(ServerEntry, false));
        var result = await _community.DevLoginAsync(phone, CancellationToken.None);
        if (result.Value is { } auth)
        {
            await _viewModel.ContinueAfterLoginAsync(auth, CancellationToken.None);
        }
        else
        {
            await DisplayAlert("Giriş yapılamadı", result.ErrorMessage ?? "Bilinmeyen hata", "Tamam");
        }
    }

    private void OnServerUnfocused(object? sender, FocusEventArgs e)
    {
        if (ApiEndpoint.TryParse(ServerEntry.Text, out var uri))
        {
            _endpoint.BaseUrl = uri;
            Preferences.Default.Set(MauiProgram.ServerUrlKey, uri.ToString());
            ServerEntry.Text = uri.ToString();
        }
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
