using MediatR;
using SahaNet.Application.Moderation;
using SahaNet.Infrastructure.Security;

namespace SahaNet.Api.Endpoints;

internal static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // Kullanıcı şikâyeti: her oturumlu kullanıcı.
        app.MapPost("/api/reports", async (ReportContentCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToHttp())
            .WithTags("Moderation")
            .RequireAuthorization()
            .WithName("ReportContent")
            .WithSummary("İlan, kullanıcı veya mesajı şikâyet et (spam, dolandırıcılık, uygunsuz, yasak av).");

        var mod = app.MapGroup("/api/admin").WithTags("Admin").RequireAuthorization(JwtTokenClaims.ModeratorPolicy);

        mod.MapGet("/dashboard", async (ISender sender, CancellationToken ct) => (await sender.Send(new GetDashboardQuery(), ct)).ToHttp())
            .WithName("GetDashboard");

        mod.MapGet("/reports", async (string? status, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetReportsQuery(status, page ?? 1, pageSize ?? 25), ct)).ToHttp())
            .WithName("GetReports");

        mod.MapPost("/reports/{id:guid}/resolve", async (Guid id, ResolveRequestBody body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ResolveReportCommand(id, body.Action, body.Note), ct)).ToHttp())
            .WithName("ResolveReport");

        mod.MapGet("/users", async (string? phone, string? q, string? status, int? page, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SearchUsersQuery(phone, q, status, page ?? 1), ct)).ToHttp())
            .WithName("SearchUsers");

        mod.MapPost("/users/{id:guid}/suspend", async (Guid id, ReasonBody body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SetUserSuspensionCommand(id, true, body.Reason), ct)).ToHttp())
            .WithName("SuspendUser");

        mod.MapPost("/users/{id:guid}/unsuspend", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SetUserSuspensionCommand(id, false, null), ct)).ToHttp())
            .WithName("UnsuspendUser");

        var admin = app.MapGroup("/api/admin").WithTags("Admin").RequireAuthorization(JwtTokenClaims.AdminPolicy);

        admin.MapPut("/users/{id:guid}/role", async (Guid id, RoleBody body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SetUserRoleCommand(id, body.Role), ct)).ToHttp())
            .WithName("SetUserRole");

        admin.MapGet("/audit", async (int? limit, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetAuditLogQuery(limit ?? 100), ct)).ToHttp())
            .WithName("GetAuditLog");

        return app;
    }
}

internal sealed record ResolveRequestBody(string Action, string? Note);

internal sealed record ReasonBody(string? Reason);

internal sealed record RoleBody(string Role);
