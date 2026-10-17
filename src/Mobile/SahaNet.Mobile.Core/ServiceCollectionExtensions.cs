using Microsoft.Extensions.DependencyInjection;
using SahaNet.Mobile.Core.Api;
using SahaNet.Mobile.Core.Data;
using SahaNet.Mobile.Core.Niche;
using SahaNet.Mobile.Core.Offline;
using SahaNet.Mobile.Core.ViewModels;

namespace SahaNet.Mobile.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>Mobil çekirdeği kaydeder. Platform servisleri (belirteç deposu, bağlantı) uygulama tarafından eklenir.</summary>
    public static IServiceCollection AddSahaNetMobileCore(
        this IServiceCollection services, ApiEndpoint endpoint, NicheProfile niche, string databasePath, Func<string>? databaseKey = null)
    {
        services.AddSingleton(niche);
        services.AddSingleton(endpoint);
        services.AddSingleton(TimeProvider.System);

        // Adres her istemci oluşturulurken okunur: kullanıcı ayarlardan sunucuyu değiştirebilir (test kurulumu).
        services.AddTransient(sp => new AuthHandler(
            sp.GetRequiredService<ITokenStore>(),
            () => new HttpClient { BaseAddress = endpoint.BaseUrl, Timeout = TimeSpan.FromSeconds(20) }));

        // Zayıf bağlantı: kısa zaman aşımı; başarısız okuma önbellekten karşılanır.
        services.AddHttpClient<ISahaNetApi, SahaNetApiClient>(http =>
            {
                http.BaseAddress = endpoint.BaseUrl;
                http.Timeout = TimeSpan.FromSeconds(15);
            })
            .AddHttpMessageHandler(sp => sp.GetRequiredService<AuthHandler>());
        services.AddTransient<ICommunityApi>(sp => (SahaNetApiClient)sp.GetRequiredService<ISahaNetApi>());

        // KVKK: yerel veritabanı cihaz anahtar deposundaki anahtarla şifrelenir (SQLCipher).
        services.AddSingleton(_ => LocalStore.OpenAsync(databasePath, databaseKey?.Invoke()).GetAwaiter().GetResult());
        services.AddSingleton<SyncService>();
        services.AddSingleton<FishingRepository>();

        services.AddTransient<OnboardingViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<SeaStatusViewModel>();
        services.AddTransient<CatchLogViewModel>();
        services.AddTransient<AlertsViewModel>();
        services.AddTransient<SpotsViewModel>();
        services.AddTransient<MarketViewModel>();
        services.AddTransient<ConversationsViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<ScrapPricesViewModel>();
        services.AddTransient<PickupsViewModel>();
        services.AddTransient<SettingsViewModel>();
        return services;
    }
}
