using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SahaNet.Api.Health;
using SahaNet.Application;
using SahaNet.Application.Modules;
using SahaNet.Infrastructure;
using SahaNet.Modules.Fishing;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services
    .AddApplication(typeof(FishingModule).Assembly)
    .AddInfrastructure(builder.Configuration)
    .AddFishingModule();

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("SahaNet.Api"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            tracing.AddOtlpExporter();
        }
    });

var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Liveness: süreç ayakta mı (bağımlılık kontrolü yok).
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthResponseWriter.WriteAsync,
});

// Readiness: PostgreSQL ve Redis erişilebilir mi.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(SahaNet.Infrastructure.DependencyInjection.ReadyTag),
    ResponseWriter = HealthResponseWriter.WriteAsync,
});

app.MapGet("/api/modules", ([FromServices] IEnumerable<IModuleDescriptor> modules) =>
        TypedResults.Ok(modules.Select(m => new { m.Key, m.DisplayName })))
    .WithName("GetModules")
    .WithTags("Platform");

app.MapFishingModule();

await app.RunAsync();

/// <summary>WebApplicationFactory ile entegrasyon testleri için görünür kılınır.</summary>
public partial class Program;
