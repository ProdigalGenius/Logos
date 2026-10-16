using SahaNet.Domain.Market;

namespace SahaNet.UnitTests.Domain.Market;

public class ListingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid Seller = Guid.NewGuid();
    private static readonly Guid BuyerA = Guid.NewGuid();
    private static readonly Guid BuyerB = Guid.NewGuid();

    private static Listing New(ListingType type, decimal price = 300, decimal quantity = 20, decimal? reserve = null, TimeSpan? duration = null) =>
        Listing.Create(Seller, "fishing", "fresh-fish", "20 kg levrek", null, type, price, quantity, "kg", "34",
            new Dictionary<string, string> { ["species"] = "levrek" }, Now + (duration ?? TimeSpan.FromDays(1)), 10, reserve, Now);

    [Fact]
    public void FixedPrice_PreordersReserveQuantity_UntilSoldOut()
    {
        var listing = New(ListingType.FixedPrice);

        var a = listing.PlaceOffer(BuyerA, 300, 5, "Sabah alırım", Now).Value;
        var b = listing.PlaceOffer(BuyerB, 300, 15, null, Now).Value;

        listing.AcceptOffer(Seller, a.Id, Now).IsSuccess.Should().BeTrue();
        listing.RemainingQuantity.Should().Be(15);
        listing.Status.Should().Be(ListingStatus.Active);

        listing.AcceptOffer(Seller, b.Id, Now).IsSuccess.Should().BeTrue();
        listing.Status.Should().Be(ListingStatus.Sold);
        a.Total.Should().Be(1500);
        listing.DomainEvents.OfType<ListingClosed>().Should().ContainSingle();
    }

    [Fact]
    public void FixedPrice_RejectsWrongPrice_ExcessQuantity_AndOwnListing()
    {
        var listing = New(ListingType.FixedPrice);

        listing.PlaceOffer(BuyerA, 250, 5, null, Now).Error.Should().Be(MarketErrors.PriceMismatch);
        listing.PlaceOffer(BuyerA, 300, 25, null, Now).Error.Should().Be(MarketErrors.QuantityUnavailable);
        listing.PlaceOffer(Seller, 300, 5, null, Now).Error.Should().Be(MarketErrors.OwnListing);
    }

    [Fact]
    public void OfferBased_AcceptingFullQuantity_RejectsOthers()
    {
        var listing = New(ListingType.OfferBased, price: 0, quantity: 1);
        var low = listing.PlaceOffer(BuyerA, 9000, 1, null, Now).Value;
        var high = listing.PlaceOffer(BuyerB, 11000, 1, null, Now).Value;

        listing.AcceptOffer(BuyerA, high.Id, Now).Error.Should().Be(MarketErrors.NotSeller);
        listing.AcceptOffer(Seller, high.Id, Now).IsSuccess.Should().BeTrue();

        low.Status.Should().Be(OfferStatus.Rejected);
        listing.DomainEvents.OfType<OfferRejected>().Should().ContainSingle(e => e.OfferId == low.Id);
        listing.AcceptOffer(Seller, low.Id, Now).Error.Should().Be(MarketErrors.ListingNotActive);
    }

    [Fact]
    public void Withdraw_OnlyByBuyer_AndNotInAuction()
    {
        var listing = New(ListingType.OfferBased);
        var offer = listing.PlaceOffer(BuyerA, 280, 2, null, Now).Value;

        listing.WithdrawOffer(BuyerB, offer.Id).Error.Should().Be(MarketErrors.NotBuyer);
        listing.WithdrawOffer(BuyerA, offer.Id).IsSuccess.Should().BeTrue();
        offer.Status.Should().Be(OfferStatus.Withdrawn);

        var auction = New(ListingType.Auction);
        var bid = auction.PlaceOffer(BuyerA, 300, 1, null, Now).Value;
        auction.WithdrawOffer(BuyerA, bid.Id).Error.Should().Be(MarketErrors.OfferNotPending);
    }

    [Fact]
    public void Auction_BidsMustBeatHighestByIncrement_AndOutbidIsNotified()
    {
        var listing = New(ListingType.Auction, price: 300);

        listing.PlaceOffer(BuyerA, 290, 1, null, Now).Error.Should().Be(MarketErrors.BidTooLow);
        var first = listing.PlaceOffer(BuyerA, 300, 1, null, Now).Value;
        first.Quantity.Should().Be(20, "ihale tüm partiyi kapsar");
        listing.PlaceOffer(BuyerB, 305, 1, null, Now).Error.Should().Be(MarketErrors.BidTooLow);
        var second = listing.PlaceOffer(BuyerB, 310, 1, null, Now).Value;

        first.Status.Should().Be(OfferStatus.Outbid);
        listing.HighestBid.Should().Be(second);
        listing.DomainEvents.OfType<BidOutbid>().Should().ContainSingle(e => e.BuyerId == BuyerA && e.NewHighest == 310);
        listing.AcceptOffer(Seller, second.Id, Now).Error.Should().Be(MarketErrors.AuctionAcceptNotAllowed);
    }

    [Fact]
    public void Auction_LateBid_ExtendsDeadline()
    {
        var listing = New(ListingType.Auction, duration: TimeSpan.FromHours(1));
        var late = listing.ExpiresAt.AddSeconds(-30);

        listing.PlaceOffer(BuyerA, 300, 1, null, late).IsSuccess.Should().BeTrue();

        listing.ExpiresAt.Should().Be(late + Listing.AntiSnipingWindow);
    }

    [Fact]
    public void Auction_Close_WinnerIfAboveReserve_ElseExpired()
    {
        var sold = New(ListingType.Auction, reserve: 320);
        sold.PlaceOffer(BuyerA, 300, 1, null, Now);
        var winner = sold.PlaceOffer(BuyerB, 330, 1, null, Now).Value;
        sold.CloseIfDue(Now).Should().BeFalse("süre dolmadı");
        sold.CloseIfDue(sold.ExpiresAt).Should().BeTrue();
        sold.Status.Should().Be(ListingStatus.Sold);
        winner.Status.Should().Be(OfferStatus.Accepted);

        var unsold = New(ListingType.Auction, reserve: 500);
        var bid = unsold.PlaceOffer(BuyerA, 300, 1, null, Now).Value;
        unsold.CloseIfDue(unsold.ExpiresAt).Should().BeTrue();
        unsold.Status.Should().Be(ListingStatus.Expired);
        bid.Status.Should().Be(OfferStatus.Rejected);
    }

    [Fact]
    public void ExpiredListing_RejectsNewOffers_AndCancelKeepsAcceptedOnes()
    {
        var listing = New(ListingType.FixedPrice);
        var accepted = listing.PlaceOffer(BuyerA, 300, 5, null, Now).Value;
        var pending = listing.PlaceOffer(BuyerB, 300, 5, null, Now).Value;
        listing.AcceptOffer(Seller, accepted.Id, Now);

        listing.Cancel(BuyerA).Error.Should().Be(MarketErrors.NotSeller);
        listing.Cancel(Seller).IsSuccess.Should().BeTrue();

        accepted.Status.Should().Be(OfferStatus.Accepted);
        pending.Status.Should().Be(OfferStatus.Rejected);
        listing.PlaceOffer(BuyerB, 300, 1, null, Now).Error.Should().Be(MarketErrors.ListingNotActive);
    }

    [Fact]
    public void Conversation_OnlyParticipantsCanSend()
    {
        var conversation = Conversation.StartDirect(Guid.Empty, BuyerA, Seller, Now);

        conversation.Send(BuyerB, MessageKind.Text, "selam", null, Now).Error.Should().Be(MarketErrors.ConversationNotFound);
        var sent = conversation.Send(BuyerA, MessageKind.Text, " Balık taze mi? ", null, Now.AddMinutes(1));

        sent.Value.Text.Should().Be("Balık taze mi?");
        conversation.LastMessageAt.Should().Be(Now.AddMinutes(1));
        conversation.ListingId.Should().BeNull();
        conversation.DomainEvents.OfType<MessageSent>().Should().ContainSingle();
    }
}
