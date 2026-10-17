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
    private const string AdminPhone = "05323330324";

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
            var detail = result.Outcome == ApiOutcome.Transient
                ? $"Sunucuya ulaşılamadı: {_endpoint.BaseUrl}\n\n• Bilgisayarda sunucu çalışıyor mu? (docker compose --profile api up -d)\n• Telefon ve bilgisayar aynı Wi-Fi'de mi?\n• Bilgisayarın güvenlik duvarı 5080 portuna izin veriyor mu?\n• Telefonun tarayıcısında {_endpoint.BaseUrl}health/live açılıyor mu?"
                : result.ErrorMessage ?? "Bilinmeyen hata";
            await DisplayAlert("Giriş yapılamadı", detail, "Tamam");
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
