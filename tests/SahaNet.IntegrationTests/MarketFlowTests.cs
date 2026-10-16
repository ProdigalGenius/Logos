using System.Net;
using System.Net.Http.Json;
using MediatR;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SahaNet.Application.Market;
using SahaNet.Modules.Fishing.Application;

namespace SahaNet.IntegrationTests;

[Collection(ApiCollection.Name)]
public class MarketFlowTests(SahaNetApiFactory factory)
{
    private static object FreshFish(string title, string type = "FixedPrice", decimal price = 450, decimal quantity = 20, DateTimeOffset? expiresAt = null) => new
    {
        categoryKey = "fresh-fish",
        title,
        description = "Sabah ağdan çıktı",
        type,
        unitPrice = price,
        quantity,
        attributes = new Dictionary<string, string>
        {
            ["species"] = "levrek",
            ["port"] = "sariyer",
            ["pickupAt"] = DateTimeOffset.UtcNow.AddDays(1).ToString("O"),
        },
        expiresAt,
        minBidIncrement = 10m,
    };

    [Fact]
    public async Task Categories_ComeFromFishingModule()
    {
        var categories = await factory.CreateClient().GetFromJsonAsync<List<CategoryResponse>>("/api/market/categories?niche=fishing");

        categories!.Select(c => c.Key).Should().BeEquivalentTo("fresh-fish", "fishing-equipment");
        categories.Single(c => c.Key == "fresh-fish").Attributes.Should().Contain(a => a.Key == "pickupAt" && a.Required);
    }

    [Fact]
    public async Task Listing_RequiresOnboarding_AndValidAttributes()
    {
        var (notOnboarded, _) = await factory.LoginAsync();
        (await notOnboarded.PostAsJsonAsync("/api/market/listings", FreshFish("Levrek"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var (seller, _) = await factory.LoginOnboardedAsync();
        var bad = await seller.PostAsJsonAsync("/api/market/listings", new
        {
            categoryKey = "fresh-fish",
            title = "Eksik alanlı",
            type = "FixedPrice",
            unitPrice = 100m,
            quantity = 1m,
            attributes = new Dictionary<string, string> { ["species"] = "levrek" },
        });
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FreshFish_Preorder_Accept_AndSearch()
    {
        var (seller, _) = await factory.LoginOnboardedAsync("Kaptan Ali");
        var (buyer, buyerAuth) = await factory.LoginOnboardedAsync("Lokantacı");

        var created = await seller.PostAsJsonAsync("/api/market/listings", FreshFish("Yarın 07:00 Sarıyer'de 20 kg levrek"));
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var listing = (await created.Content.ReadFromJsonAsync<ListingResponse>())!;
        listing.Unit.Should().Be("kg");
        listing.ShareUrl.Should().StartWith("https://wa.me/");

        var search = await factory.CreateClient().GetFromJsonAsync<ListingPage>("/api/market/listings?category=fresh-fish&q=sarıyer");
        search!.Items.Should().Contain(l => l.Id == listing.Id);

        var preorder = await buyer.PostAsJsonAsync($"/api/market/listings/{listing.Id}/offers", new { unitPrice = 450m, quantity = 5m, message = "Restoran için" });
        preorder.StatusCode.Should().Be(HttpStatusCode.OK);
        var offer = (await preorder.Content.ReadFromJsonAsync<OfferResponse>())!;
        offer.Total.Should().Be(2250m);

        (await buyer.PostAsync(new Uri($"/api/market/offers/{offer.Id}/accept", UriKind.Relative), null)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden, "yalnızca satıcı kabul edebilir");
        (await seller.PostAsync(new Uri($"/api/market/offers/{offer.Id}/accept", UriKind.Relative), null)).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        var afterAccept = await seller.GetFromJsonAsync<ListingResponse>($"/api/market/listings/{listing.Id}");
        afterAccept!.RemainingQuantity.Should().Be(15m);
        afterAccept.Offers.Should().ContainSingle().Which.Status.Should().Be("Accepted");

        var anonymous = await factory.CreateClient().GetFromJsonAsync<ListingResponse>($"/api/market/listings/{listing.Id}");
        anonymous!.Offers.Should().BeEmpty("teklif ayrıntıları yalnızca taraflara görünür");

        var myOffers = await buyer.GetFromJsonAsync<List<ListingResponse>>("/api/me/offers");
        myOffers.Should().ContainSingle(l => l.Id == listing.Id);
        buyerAuth.UserId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Auction_HighestBidWins_WhenClosed()
    {
        var (seller, _) = await factory.LoginOnboardedAsync();
        var (bidderA, _) = await factory.LoginOnboardedAsync();
        var (bidderB, authB) = await factory.LoginOnboardedAsync();

        var created = await seller.PostAsJsonAsync("/api/market/listings",
            FreshFish("Kasa palamut ihalesi", "Auction", price: 100, quantity: 50, expiresAt: DateTimeOffset.UtcNow.AddMinutes(10)));
        var listing = (await created.Content.ReadFromJsonAsync<ListingResponse>())!;

        (await bidderA.PostAsJsonAsync($"/api/market/listings/{listing.Id}/offers", new { unitPrice = 100m })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await bidderB.PostAsJsonAsync($"/api/market/listings/{listing.Id}/offers", new { unitPrice = 105m })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await bidderB.PostAsJsonAsync($"/api/market/listings/{listing.Id}/offers", new { unitPrice = 120m })).StatusCode.Should().Be(HttpStatusCode.OK);

        var view = await factory.CreateClient().GetFromJsonAsync<ListingResponse>($"/api/market/listings/{listing.Id}");
        view!.HighestBid.Should().Be(120m);
        view.NextMinimumBid.Should().Be(130m);

        // Süre dolmadan kapanış işi bir şey yapmaz.
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CloseDueListingsCommand());
        (await factory.CreateClient().GetFromJsonAsync<ListingResponse>($"/api/market/listings/{listing.Id}"))!.Status.Should().Be("Active");
        authB.UserId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Chat_DeliversInRealtime_AndTracksUnread()
    {
        var (seller, sellerAuth) = await factory.LoginOnboardedAsync("Satıcı");
        var (buyer, _) = await factory.LoginOnboardedAsync("Alıcı");
        var listing = (await (await seller.PostAsJsonAsync("/api/market/listings", FreshFish("Sohbet ilanı"))).Content.ReadFromJsonAsync<ListingResponse>())!;

        var received = new TaskCompletionSource<MessageResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/chat"), o =>
            {
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling; // TestServer WebSocket desteklemez
                o.AccessTokenProvider = () => Task.FromResult<string?>(sellerAuth.AccessToken);
            })
            .Build();
        connection.On<MessageResponse>("messageReceived", m => received.TrySetResult(m));
        await connection.StartAsync();

        var start = await buyer.PostAsJsonAsync("/api/conversations", new { listingId = listing.Id });
        var conversation = (await start.Content.ReadFromJsonAsync<ConversationResponse>())!;
        var again = (await (await buyer.PostAsJsonAsync("/api/conversations", new { listingId = listing.Id })).Content.ReadFromJsonAsync<ConversationResponse>())!;
        again.Id.Should().Be(conversation.Id, "aynı ilan için tek sohbet");

        (await buyer.PostAsJsonAsync($"/api/conversations/{conversation.Id}/messages", new { text = "Levrekler kaç gramlık?" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var pushed = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        pushed.Text.Should().Be("Levrekler kaç gramlık?");

        var inbox = await seller.GetFromJsonAsync<List<ConversationResponse>>("/api/conversations");
        inbox!.Single(c => c.Id == conversation.Id).UnreadCount.Should().Be(1);

        var messages = await seller.GetFromJsonAsync<List<MessageResponse>>($"/api/conversations/{conversation.Id}/messages");
        messages.Should().ContainSingle();
        (await seller.GetFromJsonAsync<List<ConversationResponse>>("/api/conversations"))!
            .Single(c => c.Id == conversation.Id).UnreadCount.Should().Be(0);

        var reply = await connection.InvokeAsync<MessageResponse>("SendMessage", conversation.Id, "250-300 gram");
        reply.SenderId.Should().Be(sellerAuth.UserId);

        var (outsider, _) = await factory.LoginAsync();
        (await outsider.GetAsync(new Uri($"/api/conversations/{conversation.Id}/messages", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarketPrices_UseMedianOfReports()
    {
        var reporters = new[] { 400m, 420m, 9999m };
        foreach (var price in reporters)
        {
            var (client, _) = await factory.LoginAsync();
            (await client.PostAsJsonAsync("/api/fishing/prices", new { speciesCode = "kalkan", provinceCode = "57", pricePerKg = price }))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var prices = await factory.CreateClient().GetFromJsonAsync<List<DailyPrice>>("/api/fishing/prices?species=kalkan&province=57");

        prices.Should().ContainSingle().Which.Median.Should().Be(420m);
    }
}
