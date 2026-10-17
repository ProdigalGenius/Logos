using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SahaNet.Application.Market;
using SahaNet.Application.Moderation;
using SahaNet.Application.Modules;
using SahaNet.Modules.Scrap.Application;
using SahaNet.Modules.Scrap.Endpoints;
using SahaNet.Modules.Scrap.Persistence;

namespace SahaNet.Modules.Scrap;

/// <summary>HurdaNet modülü. Tüm entegrasyon çekirdek sözleşmeleri üzerinden; çekirdekte değişiklik gerekmedi.</summary>
public static class ScrapModule
{
    public static IServiceCollection AddScrapModule(this IServiceCollection services)
    {
        services.AddSingleton<IModuleDescriptor, ScrapModuleDescriptor>();
        services.AddDbContext<ScrapDbContext>((sp, options) => options.UseNpgsql(
            sp.GetRequiredService<IConfiguration>()["Database:ConnectionString"],
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", ScrapDbContext.Schema)));

        services.AddScoped<IModuleSchemaInitializer, ScrapSchemaInitializer>();
        services.AddScoped<IPersonalDataSection, ScrapPersonalDataSection>();
        services.AddScoped<IAdminMetricsSection, ScrapAdminMetrics>();
        services.AddSingleton<IListingCategoryProvider, ScrapListingCategories>();
        return services;
    }

    public static IEndpointRouteBuilder MapScrapModule(this IEndpointRouteBuilder endpoints)
    {
        ScrapEndpoints.Map(endpoints.MapGroup($"/api/{ScrapModuleDescriptor.ModuleKey}").WithTags("Scrap"));
        return endpoints;
    }
}
