using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SahaNet.Api.Health;

/// <summary>Health check sonucunu sade JSON olarak yazar (hata detayı sızdırmaz).</summary>
internal static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            durationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = e.Value.Duration.TotalMilliseconds,
            }),
        };

        return JsonSerializer.SerializeAsync(context.Response.Body, payload, JsonOptions, context.RequestAborted);
    }
}
