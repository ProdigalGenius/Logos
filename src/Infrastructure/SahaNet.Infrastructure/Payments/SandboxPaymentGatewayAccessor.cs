namespace SahaNet.Infrastructure.Payments;

/// <summary>Api'nin sandbox ödeme simülasyonuna erişimi (sınıfın kendisi internal kalır).</summary>
public sealed class SandboxPaymentGatewayAccessor
{
    private readonly SandboxPaymentGateway _gateway;

    internal SandboxPaymentGatewayAccessor(SandboxPaymentGateway gateway) => _gateway = gateway;

    public bool SimulatePayment(string token) => _gateway.SimulatePayment(token);
}
