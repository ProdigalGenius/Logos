using System.Net;
using System.Net.Http.Json;
using SahaNet.Application.Market;
using SahaNet.Application.Payments;

namespace SahaNet.IntegrationTests;

[Collection(ApiCollection.Name)]
public class PaymentFlowTests(SahaNetApiFactory factory)
{
    private async Task<(HttpClient Seller, HttpClient Buyer, OfferResponse Offer)> AcceptedPreorderAsync(decimal quantity = 4)
    {
        var (seller, _) = await factory.LoginOnboardedAsync("Satıcı");
        var (buyer, _) = await factory.LoginOnboardedAsync("Alıcı");
        var listing = (await (await seller.PostAsJsonAsync("/api/market/listings", new
        {
            categoryKey = "fresh-fish",
            title = "Ödeme testi levrek",
            type = "FixedPrice",
            unitPrice = 500m,
            quantity = 10m,
            attributes = new Dictionary<string, string>
            {
                ["species"] = "levrek",
                ["port"] = "sariyer",
                ["pickupAt"] = DateTimeOffset.UtcNow.AddDays(1).ToString("O"),
            },
        })).Content.ReadFromJsonAsync<ListingResponse>())!;

        var offer = (await (await buyer.PostAsJsonAsync($"/api/market/listings/{listing.Id}/offers", new { unitPrice = 500m, quantity }))
            .Content.ReadFromJsonAsync<OfferResponse>())!;
        (await seller.PostAsync(new Uri($"/api/market/offers/{offer.Id}/accept", UriKind.Relative), null)).EnsureSuccessStatusCode();
        return (seller, buyer, offer);
    }

    private static string TokenOf(Uri paymentUrl) => paymentUrl.Segments[^1];

    private async Task<CheckoutResponse> PayAsync(HttpClient buyer, OfferResponse offer)
    {
        var checkout = (await (await buyer.PostAsync(new Uri($"/api/payments/offers/{offer.Id}/checkout", UriKind.Relative), null))
            .Content.ReadFromJsonAsync<CheckoutResponse>())!;
        (await factory.CreateClient().PostAsync(new Uri($"/api/payments/sandbox/{TokenOf(checkout.PaymentUrl!)}/pay", UriKind.Relative), null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        return checkout;
    }

    [Fact]
    public async Task Escrow_HappyPath_ReleasesToSellerMinusCommission()
    {
        var (seller, buyer, offer) = await AcceptedPreorderAsync();

        var checkout = await PayAsync(buyer, offer);
        checkout.Order.Amount.Should().Be(2000m);
        checkout.Order.Commission.Should().Be(0, "komisyon alıcıya gösterilmez");

        var again = (await (await buyer.PostAsync(new Uri($"/api/payments/offers/{offer.Id}/checkout", UriKind.Relative), null))
            .Content.ReadFromJsonAsync<CheckoutResponse>())!;
        again.Order.Id.Should().Be(checkout.Order.Id);
        again.PaymentUrl.Should().BeNull("zaten ödendi");

        (await buyer.PostAsync(new Uri($"/api/payments/orders/{checkout.Order.Id}/delivered", UriKind.Relative), null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await seller.PostAsync(new Uri($"/api/payments/orders/{checkout.Order.Id}/delivered", UriKind.Relative), null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var confirmed = await buyer.PostAsync(new Uri($"/api/payments/orders/{checkout.Order.Id}/confirm", UriKind.Relative), null);
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await confirmed.Content.ReadFromJsonAsync<PaymentOrderResponse>())!.Status.Should().Be("Released");

        var sellerView = (await seller.GetFromJsonAsync<List<PaymentOrderResponse>>("/api/me/payments"))!.Single(o => o.Id == checkout.Order.Id);
        sellerView.Commission.Should().Be(100m);
        sellerView.SellerPayout.Should().Be(1900m);
        sellerView.Role.Should().Be("Seller");
    }

    [Fact]
    public async Task Dispute_IsResolvedByAdmin_WithRefund()
    {
        var (seller, buyer, offer) = await AcceptedPreorderAsync(quantity: 2);
        var checkout = await PayAsync(buyer, offer);
        await seller.PostAsync(new Uri($"/api/payments/orders/{checkout.Order.Id}/delivered", UriKind.Relative), null);

        (await buyer.PostAsJsonAsync($"/api/payments/orders/{checkout.Order.Id}/dispute", new { reason = "Balıklar bayat geldi" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await buyer.PostAsync(new Uri($"/api/payments/orders/{checkout.Order.Id}/confirm", UriKind.Relative), null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "itirazdaki ödeme onaylanamaz");

        (await seller.GetAsync(new Uri("/api/admin/payments/disputes", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var (admin, _) = await factory.LoginAsync(SahaNetApiFactory.AdminPhone);
        var disputes = await admin.GetFromJsonAsync<List<PaymentOrderResponse>>("/api/admin/payments/disputes");
        disputes.Should().Contain(d => d.Id == checkout.Order.Id && d.DisputeReason == "Balıklar bayat geldi");

        var resolved = await admin.PostAsJsonAsync($"/api/admin/payments/orders/{checkout.Order.Id}/resolve", new { decision = "RefundBuyer", note = "Alıcı haklı" });
        resolved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resolved.Content.ReadFromJsonAsync<PaymentOrderResponse>())!.Status.Should().Be("Refunded");
    }

    [Fact]
    public async Task Checkout_RequiresAcceptedOffer_ByTheBuyer()
    {
        var (seller, buyer, offer) = await AcceptedPreorderAsync();

        (await seller.PostAsync(new Uri($"/api/payments/offers/{offer.Id}/checkout", UriKind.Relative), null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await factory.CreateClient().PostAsJsonAsync("/api/payments/callback", new { token = "sahte" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "doğrulanamayan geri çağrı kabul edilmez");
        buyer.Should().NotBeNull();
    }
}
