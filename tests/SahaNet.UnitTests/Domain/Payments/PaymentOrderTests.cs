using SahaNet.Domain.Payments;

namespace SahaNet.UnitTests.Domain.Payments;

public class PaymentOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid Buyer = Guid.NewGuid();
    private static readonly Guid Seller = Guid.NewGuid();
    private static readonly TimeSpan DisputeWindow = TimeSpan.FromHours(48);

    private static PaymentOrder New(decimal amount = 2250m) =>
        PaymentOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Buyer, Seller, amount, 0.05m, 1m, TimeSpan.FromMinutes(30), Now);

    [Theory]
    [InlineData(2250, 0.05, 1, 112.50)]
    [InlineData(10, 0.05, 1, 1)]
    [InlineData(0.5, 0.05, 1, 0.5)]
    [InlineData(333.33, 0.03, 0, 10)]
    public void Commission_RateWithMinimum_NeverExceedsAmount(decimal amount, decimal rate, decimal minimum, decimal expected)
    {
        PaymentOrder.CalculateCommission(amount, rate, minimum).Should().Be(expected);
    }

    [Fact]
    public void HappyPath_PaidDeliveredConfirmed_ReleasesPayout()
    {
        var order = New();

        order.MarkPaid("p-1", 2250m, Now).IsSuccess.Should().BeTrue();
        order.ReportDelivered(Seller, DisputeWindow, Now.AddHours(20)).IsSuccess.Should().BeTrue();
        order.ConfirmReceipt(Buyer, Now.AddHours(21)).IsSuccess.Should().BeTrue();

        order.Status.Should().Be(PaymentStatus.Released);
        order.SellerPayout.Should().Be(2137.50m);
        order.DomainEvents.OfType<PaymentReleased>().Should().ContainSingle(e => e.Payout == 2137.50m && e.Commission == 112.50m);
    }

    [Fact]
    public void MarkPaid_IsIdempotent_AndChecksAmount()
    {
        var order = New();

        order.MarkPaid("p-1", 2000m, Now).Error.Should().Be(PaymentErrors.AmountMismatch);
        order.MarkPaid("p-1", 2250m, Now).IsSuccess.Should().BeTrue();
        order.MarkPaid("p-1", 2250m, Now).IsSuccess.Should().BeTrue("aynı geri çağrı tekrar gelebilir");
        order.MarkPaid("p-2", 2250m, Now).Error.Should().Be(PaymentErrors.InvalidState);
        order.DomainEvents.OfType<PaymentHeld>().Should().ContainSingle();
    }

    [Fact]
    public void RolesAreEnforced()
    {
        var order = New();
        order.MarkPaid("p-1", 2250m, Now);

        order.ReportDelivered(Buyer, DisputeWindow, Now).Error.Should().Be(PaymentErrors.NotSeller);
        order.ConfirmReceipt(Seller, Now).Error.Should().Be(PaymentErrors.NotBuyer);
        order.OpenDispute(Seller, "olmadı", Now).Error.Should().Be(PaymentErrors.NotBuyer);
    }

    [Fact]
    public void Dispute_WithinWindow_ThenRefund_WaivesCommission()
    {
        var order = New();
        order.MarkPaid("p-1", 2250m, Now);
        order.ReportDelivered(Seller, DisputeWindow, Now);

        order.OpenDispute(Buyer, "Balıklar bozuk geldi", Now.AddHours(5)).IsSuccess.Should().BeTrue();
        order.AutoReleaseIfDue(Now.AddDays(5)).Should().BeFalse("itirazdaki ödeme otomatik serbest bırakılmaz");

        var admin = Guid.NewGuid();
        order.Resolve(admin, DisputeDecision.RefundBuyer, "Fotoğraflar haklı", Now.AddDays(1)).IsSuccess.Should().BeTrue();

        order.Status.Should().Be(PaymentStatus.Refunded);
        order.Commission.Should().Be(0);
        order.DecidedBy.Should().Be(admin);
        order.DomainEvents.OfType<PaymentRefunded>().Should().ContainSingle();
    }

    [Fact]
    public void Dispute_AfterWindow_IsRejected_AndAutoReleaseHappens()
    {
        var order = New();
        order.MarkPaid("p-1", 2250m, Now);
        order.ReportDelivered(Seller, DisputeWindow, Now);

        order.OpenDispute(Buyer, "Geç kaldım", Now + DisputeWindow).Error.Should().Be(PaymentErrors.DisputeWindowClosed);
        order.AutoReleaseIfDue(Now.AddHours(47)).Should().BeFalse();
        order.AutoReleaseIfDue(Now + DisputeWindow).Should().BeTrue();
        order.Status.Should().Be(PaymentStatus.Released);
    }

    [Fact]
    public void Unpaid_IsCancelledAfterWindow()
    {
        var order = New();

        order.CancelIfUnpaid(Now.AddMinutes(29)).Should().BeFalse();
        order.CancelIfUnpaid(Now.AddMinutes(30)).Should().BeTrue();

        order.Status.Should().Be(PaymentStatus.Cancelled);
        order.MarkPaid("p-late", 2250m, Now.AddMinutes(31)).Error.Should().Be(PaymentErrors.InvalidState);
    }
}
