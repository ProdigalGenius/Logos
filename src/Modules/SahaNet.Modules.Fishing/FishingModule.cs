using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SahaNet.Application.Modules;

namespace SahaNet.Modules.Fishing;

/// <summary>Balıkçı modülünün tek giriş noktası. Api bu iki metodu çağırır.</summary>
public static class FishingModule
{
    public static IServiceCollection AddFishingModule(this IServiceCollection services)
    {
        services.AddSingleton<IModuleDescriptor, FishingModuleDescriptor>();
        return services;
    }

    public static IEndpointRouteBuilder MapFishingModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"/api/{FishingModuleDescriptor.ModuleKey}")
            .WithTags("Fishing");

        // Faz 0: modülün yüklendiğini doğrulayan basit uç nokta. Faz 3'te gerçek özellikler gelecek.
        group.MapGet("/info", () => TypedResults.Ok(new ModuleInfoResponse(FishingModuleDescriptor.ModuleKey, "BalıkçıNet")))
            .WithName("GetFishingModuleInfo");

        return endpoints;
    }
}

public sealed record ModuleInfoResponse(string Key, string DisplayName);
