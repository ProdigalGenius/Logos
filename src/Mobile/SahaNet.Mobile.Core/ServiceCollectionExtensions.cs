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
    public static IServiceCollection AddSahaNetMobileCore(this IServiceCollection services, Uri apiBaseUrl, NicheProfile niche, string databasePath)
    {
        services.AddSingleton(niche);
        services.AddSingleton(TimeProvider.System);

        services.AddTransient(sp => new AuthHandler(
            sp.GetRequiredService<ITokenStore>(),
            () => new HttpClient { BaseAddress = apiBaseUrl, Timeout = TimeSpan.FromSeconds(20) }));

        // Zayıf bağlantı: kısa zaman aşımı; başarısız okuma önbellekten karşılanır.
        services.AddHttpClient<ISahaNetApi, SahaNetApiClient>(http =>
            {
                http.BaseAddress = apiBaseUrl;
                http.Timeout = TimeSpan.FromSeconds(15);
            })
            .AddHttpMessageHandler(sp => sp.GetRequiredService<AuthHandler>());

        services.AddSingleton(_ => LocalStore.OpenAsync(databasePath).GetAwaiter().GetResult());
        services.AddSingleton<SyncService>();
        services.AddSingleton<FishingRepository>();

        services.AddTransient<OnboardingViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<SeaStatusViewModel>();
        services.AddTransient<CatchLogViewModel>();
        return services;
    }
}
