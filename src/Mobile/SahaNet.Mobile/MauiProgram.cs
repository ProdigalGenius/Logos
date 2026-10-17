using System.Reflection;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SahaNet.Mobile.Core;
using SahaNet.Mobile.Core.Api;
using SahaNet.Mobile.Core.Niche;
using SahaNet.Mobile.Core.Offline;
using SahaNet.Mobile.Services;
using SahaNet.Mobile.Views;

namespace SahaNet.Mobile;

public static class MauiProgram
{
    /// <summary>API adresi. Geliştirmede Android emülatörü için 10.0.2.2; yayında niş alan adı.</summary>
    private static readonly Uri ApiBaseUrl = new(
#if DEBUG
        DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:5080/" : "http://localhost:5080/"
#else
        "https://api.sahanet.app/"
#endif
    );

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit();

        using var nicheStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("niche.json")
            ?? throw new InvalidOperationException("niche.json gömülü kaynak bulunamadı.");
        var niche = NicheProfile.Load(nicheStream);

        builder.Services.AddSingleton<ITokenStore, SecureTokenStore>();
        builder.Services.AddSingleton<IConnectivityMonitor, MauiConnectivityMonitor>();
        builder.Services.AddSingleton<IVoiceInput, VoiceInput>();
        builder.Services.AddSahaNetMobileCore(ApiBaseUrl, niche, Path.Combine(FileSystem.AppDataDirectory, "sahanet.db3"));

        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<OnboardingPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<SeaStatusPage>();
        builder.Services.AddTransient<CatchLogPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
