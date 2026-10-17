using SahaNet.Mobile.Core.Api;
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
        // Oturum yoksa kurulum (3 adım), varsa ana ekran.
        var tokens = _services.GetRequiredService<ITokenStore>().GetAsync().GetAwaiter().GetResult();
        Page root = tokens is null
            ? _services.GetRequiredService<OnboardingPage>()
            : _services.GetRequiredService<AppShell>();
        return new Window(root);
    }

    public void ShowShell() => Windows[0].Page = _services.GetRequiredService<AppShell>();

    public void ShowOnboarding() => Windows[0].Page = _services.GetRequiredService<OnboardingPage>();
}
