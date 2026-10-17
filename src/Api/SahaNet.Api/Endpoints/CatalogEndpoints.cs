using MediatR;
using Microsoft.AspNetCore.Mvc;
using SahaNet.Application.Common;
using SahaNet.Application.Consents;
using SahaNet.Application.Modules;

namespace SahaNet.Api.Endpoints;

/// <summary>Giriş öncesi de erişilebilen katalog uçları (nişler, bölgeler, KVKK metinleri).</summary>
internal static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Catalog").AllowAnonymous();

        group.MapGet("/modules", ([FromServices] IEnumerable<IModuleDescriptor> modules) =>
                TypedResults.Ok(modules.Select(m => new { m.Key, m.DisplayName })))
            .WithName("GetModules");

        group.MapGet("/regions", () => TypedResults.Ok(RegionCatalog.All))
            .WithName("GetRegions");

        group.MapGet("/consents/documents", async (ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new GetConsentDocumentsQuery(), ct)))
            .WithName("GetConsentDocuments");

        return app;
    }
}
