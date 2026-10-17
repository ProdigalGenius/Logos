using SahaNet.Mobile.Core.Api;
using SahaNet.Mobile.Core.Offline;
using SahaNet.Mobile.Views;

namespace SahaNet.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Arayüz iş parçacığı bekletilmez (Android'de güvenli depo okuması kilitlenmeye yol açar):
        // önce yükleniyor ekranı, oturum arka planda okunduktan sonra kurulum veya ana ekran.
        var window = new Window(new ContentPage
        {
            Content = new ActivityIndicator { IsRunning = true, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center },
        });
        window.Created += async (_, _) => await ShowStartPageAsync(window);
        return window;
    }

    private async Task ShowStartPageAsync(Window window)
    {
        Page root;
        try
        {
            var tokens = await Task.Run(() => _services.GetRequiredService<ITokenStore>().GetAsync());
            // Şifreli yerel veritabanı (anahtar okuma + açılış) arka planda hazırlanır; sayfalar UI iş parçacığında kurulur.
            await Task.Run(() => _services.GetRequiredService<LocalStore>());
            root = tokens is null
                ? _services.GetRequiredService<OnboardingPage>()
                : _services.GetRequiredService<AppShell>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Açılış hatası sessizce takılı kalmasın: kullanıcıya görünür mesaj.
            root = new ContentPage
            {
                Content = new Label { Text = $"Uygulama başlatılamadı:\n{ex.GetType().Name}: {ex.Message}", Margin = 24, VerticalOptions = LayoutOptions.Center },
            };
        }

        window.Page = root;
    }

    public void ShowShell() => Windows[0].Page = _services.GetRequiredService<AppShell>();

    public void ShowOnboarding() => Windows[0].Page = _services.GetRequiredService<OnboardingPage>();
}
