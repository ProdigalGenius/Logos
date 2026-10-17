using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SahaNet.Mobile.Core.Api;

namespace SahaNet.Mobile.Core.ViewModels;

/// <summary>Ayarlar: sunucu adresi (test), KVKK haklarım (verilerimi indir, hesabımı sil), çıkış.</summary>
public sealed partial class SettingsViewModel(ICommunityApi api, ITokenStore tokens, ApiEndpoint endpoint) : ObservableObject
{
    [ObservableProperty]
    private string _serverUrl = endpoint.BaseUrl.ToString();

    [ObservableProperty]
    private string? _statusText;

    [ObservableProperty]
    private bool _isAdmin;

    [ObservableProperty]
    private string? _newUserPhone;

    [ObservableProperty]
    private bool _newUserIsModerator;

    /// <summary>Platform tarafı adresi kalıcı saklar.</summary>
    public event EventHandler<Uri>? ServerChanged;

    public event EventHandler? SignedOut;

    /// <summary>Dışa aktarılan JSON; platform tarafı paylaşım penceresiyle sunar.</summary>
    public event EventHandler<string>? ExportReady;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsAdmin = JwtSubject.ReadRole((await tokens.GetAsync())?.AccessToken) == "Admin";
    }

    [RelayCommand]
    private async Task AddUserAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(NewUserPhone))
        {
            StatusText = "Telefon numarası girin.";
            return;
        }

        var result = await api.CreateUserAsync(NewUserPhone, NewUserIsModerator ? "Moderator" : "User", cancellationToken);
        StatusText = result.IsSuccess
            ? $"Kullanıcı eklendi: {NewUserPhone}. Bu numarayla \"Kod gönder\" ile giriş yapılabilir."
            : result.ErrorMessage;
        if (result.IsSuccess)
        {
            NewUserPhone = null;
            NewUserIsModerator = false;
        }
    }

    [RelayCommand]
    private void SaveServer()
    {
        if (!ApiEndpoint.TryParse(ServerUrl, out var uri))
        {
            StatusText = "Geçersiz adres. Örnek: 192.168.1.20:5080";
            return;
        }

        endpoint.BaseUrl = uri;
        ServerUrl = uri.ToString();
        ServerChanged?.Invoke(this, uri);
        StatusText = "Sunucu adresi kaydedildi.";
    }

    [RelayCommand]
    private async Task ExportAsync(CancellationToken cancellationToken)
    {
        var result = await api.ExportMyDataAsync(cancellationToken);
        if (result.Value is { } json)
        {
            ExportReady?.Invoke(this, json);
            StatusText = null;
        }
        else
        {
            StatusText = result.ErrorMessage;
        }
    }

    [RelayCommand]
    private async Task DeleteAccountAsync(CancellationToken cancellationToken)
    {
        var result = await api.RequestAccountDeletionAsync(cancellationToken);
        StatusText = result.IsSuccess
            ? "Hesabınız silinmek üzere işaretlendi. 30 gün içinde tekrar giriş yaparak vazgeçebilirsiniz."
            : result.ErrorMessage;
        if (result.IsSuccess)
        {
            await SignOutAsync();
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await tokens.ClearAsync();
        SignedOut?.Invoke(this, EventArgs.Empty);
    }
}
