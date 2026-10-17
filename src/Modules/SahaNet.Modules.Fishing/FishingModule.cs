using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SahaNet.Application.Market;
using SahaNet.Application.Modules;
using SahaNet.Modules.Fishing.Application;
using SahaNet.Modules.Fishing.Endpoints;
using SahaNet.Modules.Fishing.Persistence;

namespace SahaNet.Modules.Fishing;

/// <summary>Balıkçı modülünün tek giriş noktası. Api yalnızca bu iki metodu çağırır.</summary>
public static class FishingModule
{
    public static IServiceCollection AddFishingModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IModuleDescriptor, FishingModuleDescriptor>();
        services.AddOptions<FishingOptions>()
            .BindConfiguration(FishingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Modül kendi bağlamını aynı veritabanında, kendi şemasında yönetir.
        // Bağlantı dizesi çalışma anında okunur (test/ortam geçersiz kılmaları kayıttan sonra uygulanabilir).
        services.AddDbContext<FishingDbContext>((sp, options) => options.UseNpgsql(
            sp.GetRequiredService<IConfiguration>()["Database:ConnectionString"],
            npgsql => npgsql
                .UseNetTopologySuite()
                .MigrationsHistoryTable("__ef_migrations_history", FishingDbContext.Schema)));

        services.AddScoped<IModuleSchemaInitializer, FishingSchemaInitializer>();
        services.AddScoped<IPersonalDataSection, FishingPersonalDataSection>();
        services.AddSingleton<IListingCategoryProvider, FishingListingCategories>();
        services.AddScoped<SahaNet.Application.Moderation.IAdminMetricsSection, FishingAdminMetrics>();

        return services;
    }

    public static IEndpointRouteBuilder MapFishingModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"/api/{FishingModuleDescriptor.ModuleKey}").WithTags("Fishing");
        FishingEndpoints.Map(group);
        return endpoints;
    }
}

public sealed record ModuleInfoResponse(string Key, string DisplayName);
