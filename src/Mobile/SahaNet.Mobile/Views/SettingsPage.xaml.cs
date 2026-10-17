using SahaNet.Mobile.Core.ViewModels;

namespace SahaNet.Mobile.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.ServerChanged += (_, uri) => Preferences.Default.Set(MauiProgram.ServerUrlKey, uri.ToString());
        _viewModel.SignedOut += (_, _) => ((App)Application.Current!).ShowOnboarding();
        _viewModel.ExportReady += async (_, json) =>
        {
            var path = Path.Combine(FileSystem.CacheDirectory, "verilerim.json");
            await File.WriteAllTextAsync(path, json);
            await Share.Default.RequestAsync(new ShareFileRequest("Verilerim", new ShareFile(path, "application/json")));
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (await DisplayAlert("Hesabı sil", "Hesabınız 30 gün sonra kalıcı olarak silinecek. Emin misiniz?", "Evet, sil", "Vazgeç"))
        {
            await _viewModel.DeleteAccountCommand.ExecuteAsync(null);
        }
    }
}
