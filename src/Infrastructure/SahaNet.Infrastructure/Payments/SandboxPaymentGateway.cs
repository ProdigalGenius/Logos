using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SahaNet.Application.Market;
using SahaNet.Application.Payments;
using SahaNet.Domain.Payments;

namespace SahaNet.Infrastructure.Payments;

/// <summary>
/// Geliştirme/test için ödeme sağlayıcısı taklidi. Gerçek para hareketi yoktur. Ödeme sayfası yerine
/// <c>POST /api/payments/sandbox/{token}/pay</c> çağrısı ödemeyi başarılı sayar.
/// Üretimde açılmaz (Api açılışta kontrol eder).
/// </summary>
internal sealed partial class SandboxPaymentGateway(IOptions<MarketOptions> market, ILogger<SandboxPaymentGateway> logger) : IPaymentGateway
{
    private readonly ConcurrentDictionary<string, SandboxSession> _sessions = new(StringComparer.Ordinal);

    public Task<CheckoutSession> CreateCheckoutAsync(PaymentOrder order, string description, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        _sessions[token] = new SandboxSession(order.Id, order.Amount, Paid: false);
        var url = new Uri($"{market.Value.PublicBaseUrl.TrimEnd('/')}/sandbox-pay/{token}");
        return Task.FromResult(new CheckoutSession(token, url));
    }

    /// <summary>Kullanıcının ödeme sayfasında "öde"ye basmasını taklit eder.</summary>
    public bool SimulatePayment(string token)
    {
        if (!_sessions.TryGetValue(token, out var session))
        {
            return false;
        }

        _sessions[token] = session with { Paid = true };
        return true;
    }

    public Task<VerifiedPayment> VerifyAsync(string token, CancellationToken cancellationToken) =>
        Task.FromResult(_sessions.TryGetValue(token, out var s) && s.Paid
            ? new VerifiedPayment(true, s.OrderId, "sandbox-" + token, s.Amount)
            : new VerifiedPayment(false, Guid.Empty, string.Empty, 0));

    public Task ReleaseAsync(PaymentOrder order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        LogRelease(logger, order.Id, order.SellerPayout, order.Commission);
        return Task.CompletedTask;
    }

    public Task RefundAsync(PaymentOrder order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        LogRefund(logger, order.Id, order.Amount);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SANDBOX: {OrderId} satıcıya aktarıldı ({Payout} TL, komisyon {Commission} TL)")]
    private static partial void LogRelease(ILogger logger, Guid orderId, decimal payout, decimal commission);

    [LoggerMessage(Level = LogLevel.Information, Message = "SANDBOX: {OrderId} iade edildi ({Amount} TL)")]
    private static partial void LogRefund(ILogger logger, Guid orderId, decimal amount);

    private sealed record SandboxSession(Guid OrderId, decimal Amount, bool Paid);
}
