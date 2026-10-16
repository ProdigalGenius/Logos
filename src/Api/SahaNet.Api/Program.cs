using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SahaNet.Api.Endpoints;
using SahaNet.Api.Health;
using SahaNet.Api.Infrastructure;
using SahaNet.Application;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Infrastructure;
using SahaNet.Infrastructure.Options;
using SahaNet.Modules.Fishing;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.With<PhoneMaskingEnricher>());

builder.Services
    .AddApplication(typeof(FishingModule).Assembly)
    .AddInfrastructure(builder.Configuration)
    .AddFishingModule();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.ConfigureOptions<ConfigureJwtBearer>();
builder.Services.AddAuthorization();

// OTP uç noktaları için IP bazlı ek koruma (numara bazlı limitler Application katmanında).
var otpPermitPerMinute = builder.Configuration.GetValue("RateLimiting:OtpPermitPerMinute", 10);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthEndpoints.OtpRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = otpPermitPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<ConcurrencyExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    options.AddSecurityDefinition("Bearer", scheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = [] });
});

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

var jobOptions = app.Services.GetRequiredService<IOptions<BackgroundJobOptions>>().Value;
if (jobOptions.Enabled && jobOptions.DashboardEnabled)
{
    // Yalnızca sunucunun kendisinden erişilebilir; Faz 7'de yönetici rolüne bağlanacak.
    app.UseHangfireDashboard("/hangfire", new DashboardOptions { Authorization = [new LocalRequestsOnlyAuthorizationFilter()] });
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

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

app.MapCatalogEndpoints();
app.MapAuthEndpoints();
app.MapMeEndpoints();
app.MapAlertEndpoints();
app.MapFishingModule();

await app.RunAsync();

/// <summary>WebApplicationFactory ile entegrasyon testleri için görünür kılınır.</summary>
public partial class Program;
