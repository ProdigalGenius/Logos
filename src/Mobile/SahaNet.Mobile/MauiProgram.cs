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
    /// <summary>Kullanıcının ayarlardan girdiği sunucu adresinin saklandığı anahtar.</summary>
    public const string ServerUrlKey = "sahanet.server_url";

    private const string DatabaseKeyName = "sahanet.db_key";

    /// <summary>Varsayılan API adresi. Geliştirmede Android emülatörü için 10.0.2.2; yayında niş alan adı.</summary>
    private static readonly Uri DefaultApiBaseUrl = new(
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
        var endpoint = new ApiEndpoint(
            ApiEndpoint.TryParse(Preferences.Default.Get<string?>(ServerUrlKey, null), out var saved) ? saved : DefaultApiBaseUrl);
        builder.Services.AddSahaNetMobileCore(endpoint, niche, Path.Combine(FileSystem.AppDataDirectory, "sahanet.db3"), GetOrCreateDatabaseKey);

        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<OnboardingPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<SeaStatusPage>();
        builder.Services.AddTransient<CatchLogPage>();
        builder.Services.AddTransient<AlertsPage>();
        builder.Services.AddTransient<SpotsPage>();
        builder.Services.AddTransient<MarketPage>();
        builder.Services.AddTransient<ConversationsPage>();
        builder.Services.AddTransient<ChatPage>();
        builder.Services.AddTransient<ScrapPricesPage>();
        builder.Services.AddTransient<PickupsPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }

    /// <summary>Yerel veritabanı anahtarı ilk açılışta üretilir ve cihazın güvenli deposunda (Keystore/Keychain) tutulur.</summary>
    private static string GetOrCreateDatabaseKey()
    {
        var key = SecureStorage.Default.GetAsync(DatabaseKeyName).GetAwaiter().GetResult();
        if (string.IsNullOrEmpty(key))
        {
            key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            SecureStorage.Default.SetAsync(DatabaseKeyName, key).GetAwaiter().GetResult();
        }

        return key;
    }
}
