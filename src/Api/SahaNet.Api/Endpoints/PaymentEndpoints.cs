using MediatR;
using SahaNet.Application.Payments;
using SahaNet.Infrastructure.Security;

namespace SahaNet.Api.Endpoints;

internal static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app, bool sandboxEnabled)
    {
        var payments = app.MapGroup("/api/payments").WithTags("Payments");

        payments.MapPost("/offers/{offerId:guid}/checkout", async (Guid offerId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new StartPaymentCommand(offerId), ct)).ToHttp())
            .RequireAuthorization()
            .WithName("StartPayment")
            .WithSummary("Kabul edilmiş teklif için emanetli ödeme başlatır (idempotent); ödeme sayfası adresini döner.");

        // Sağlayıcının geri çağrısı: kimlik doğrulaması yok, durum sağlayıcıdan doğrulanır.
        payments.MapPost("/callback", async (CallbackRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CompletePaymentCommand(body.Token), ct)).ToHttp())
            .AllowAnonymous()
            .WithName("PaymentCallback");

        payments.MapPost("/orders/{id:guid}/delivered", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new PaymentActionCommand(id, PaymentAction.ReportDelivered), ct)).ToHttp())
            .RequireAuthorization()
            .WithName("ReportDelivered");

        payments.MapPost("/orders/{id:guid}/confirm", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new PaymentActionCommand(id, PaymentAction.ConfirmReceipt), ct)).ToHttp())
            .RequireAuthorization()
            .WithName("ConfirmReceipt")
            .WithSummary("Alıcı teslim aldığını onaylar; para komisyon düşülerek satıcıya geçer.");

        payments.MapPost("/orders/{id:guid}/dispute", async (Guid id, DisputeRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new OpenDisputeCommand(id, body.Reason), ct)).ToHttp())
            .RequireAuthorization()
            .WithName("OpenDispute");

        app.MapGet("/api/me/payments", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetMyPaymentsQuery(), ct)).ToHttp())
            .WithTags("Me")
            .RequireAuthorization()
            .WithName("GetMyPayments");

        var admin = app.MapGroup("/api/admin/payments").WithTags("Admin").RequireAuthorization(JwtTokenClaims.AdminPolicy);

        admin.MapGet("/disputes", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetDisputesQuery(), ct)).ToHttp())
            .WithName("GetDisputes");

        admin.MapPost("/orders/{id:guid}/resolve", async (Guid id, ResolveRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ResolveDisputeCommand(id, body.Decision, body.Note), ct)).ToHttp())
            .WithName("ResolveDispute");

        admin.MapGet("/commission", async (DateTimeOffset from, DateTimeOffset to, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetCommissionSummaryQuery(from, to), ct)).ToHttp())
            .WithName("GetCommissionSummary");

        if (sandboxEnabled)
        {
            payments.MapPost("/sandbox/{token}/pay", async (string token, SahaNet.Infrastructure.Payments.SandboxPaymentGatewayAccessor sandbox, ISender sender, CancellationToken ct) =>
                    sandbox.SimulatePayment(token)
                        ? (await sender.Send(new CompletePaymentCommand(token), ct)).ToHttp()
                        : Results.NotFound())
                .AllowAnonymous()
                .WithName("SandboxPay")
                .WithSummary("YALNIZCA GELİŞTİRME: ödeme sayfasında ödeme yapılmış gibi davranır.");
        }

        return app;
    }
}

internal sealed record CallbackRequest(string Token);

internal sealed record DisputeRequest(string Reason);

internal sealed record ResolveRequest(string Decision, string? Note);
