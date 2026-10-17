using SahaNet.Application.Moderation;
using SahaNet.Domain.Identity;
using SahaNet.Domain.Market;
using SahaNet.Domain.Moderation;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace SahaNet.UnitTests.Domain;

public class ModerationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);
    private readonly RuleBasedSpamFilter _filter = new(MsOptions.Create(new SpamOptions()));

    [Fact]
    public void Spam_NormalListingWithPhoneNumber_IsAllowed()
    {
        var result = _filter.Evaluate(SpamContent.Listing, "Sahibinden 7 metre tekne, motorlu. Ara: 0532 123 45 67", []);

        result.Verdict.Should().Be(SpamVerdict.Allow, "telefon numarası hedef kitlede olağandır");
    }

    [Fact]
    public void Spam_BlockedWordsAndLinks_AreBlocked()
    {
        var result = _filter.Evaluate(SpamContent.Listing, "Bahis siteleri burada http://x.xyz", []);

        result.Verdict.Should().Be(SpamVerdict.Block);
        result.Reasons.Should().Contain(r => r.Contains("bahis")).And.Contain(r => r.Contains("link"));
    }

    [Fact]
    public void Spam_SingleLink_IsReview_ButWhatsAppLinkIsFine()
    {
        _filter.Evaluate(SpamContent.Message, "Fotoğraflar www.ornek.com adresinde", []).Verdict.Should().Be(SpamVerdict.Review);
        _filter.Evaluate(SpamContent.Message, "Buradan yaz: https://wa.me/905321234567", []).Verdict.Should().Be(SpamVerdict.Allow);
    }

    [Fact]
    public void Spam_DuplicateListing_AndShouting_AddUp()
    {
        var result = _filter.Evaluate(SpamContent.Listing, "ACİL SATILIK SÜPER AĞ KAÇIRMAYIN!!!!!!", ["ACİL SATILIK SÜPER AĞ KAÇIRMAYIN!!!!!!"]);

        result.Verdict.Should().Be(SpamVerdict.Block);
        result.Score.Should().BeGreaterThanOrEqualTo(7);
    }

    [Fact]
    public void User_Suspend_AndUnsuspend()
    {
        var user = User.Register(PhoneNumber.Create("05321234567").Value, "h", Now);

        user.Suspend("Dolandırıcılık").IsSuccess.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Suspended);
        user.DomainEvents.OfType<UserSuspended>().Should().ContainSingle();
        user.Suspend("tekrar").Error.Should().Be(IdentityErrors.AccountSuspended);

        user.Unsuspend().IsSuccess.Should().BeTrue();
        user.SuspensionReason.Should().BeNull();
        user.Unsuspend().Error.Should().Be(IdentityErrors.AccountNotSuspended);
    }

    [Fact]
    public void Listing_RemovedByModerator_RejectsPendingOffers()
    {
        var listing = Listing.Create(Guid.NewGuid(), "fishing", "fishing-equipment", "Ağ", null, ListingType.OfferBased, 0, 1, "adet", null,
            new Dictionary<string, string>(), Now.AddDays(1), null, null, Now);
        var offer = listing.PlaceOffer(Guid.NewGuid(), 100, 1, null, Now).Value;

        listing.RemoveByModerator("Sahte ilan", Now).IsSuccess.Should().BeTrue();

        listing.Status.Should().Be(ListingStatus.Removed);
        listing.ModerationNote.Should().Be("Sahte ilan");
        offer.Status.Should().Be(OfferStatus.Rejected);
        listing.RemoveByModerator("tekrar", Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Report_ResolvesOnce()
    {
        var report = ContentReport.ByUser(Guid.NewGuid(), ReportTargetType.Message, Guid.NewGuid(), Guid.NewGuid(), ReportReason.Inappropriate, "  hakaret ", Now);
        report.Note.Should().Be("hakaret");

        report.Resolve(Guid.NewGuid(), ModerationAction.Dismiss, null, Now).IsSuccess.Should().BeTrue();
        report.Status.Should().Be(ReportStatus.Dismissed);
        report.Resolve(Guid.NewGuid(), ModerationAction.RemoveContent, null, Now).Error.Should().Be(ModerationErrors.AlreadyResolved);
    }

    [Fact]
    public void Message_Redact_RemovesContent()
    {
        var conversation = Conversation.StartDirect(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Now);
        var sender = conversation.Participants.First().UserId;
        var message = conversation.Send(sender, MessageKind.Photo, "bak", "https://cdn.test/x.jpg", Now).Value;

        message.Redact();

        message.IsRedacted.Should().BeTrue();
        message.MediaUrl.Should().BeNull();
        message.Text.Should().Contain("kaldırıldı");
    }

    [Fact]
    public void Audit_TruncatesLongDetails()
    {
        AuditEntry.Record(Guid.NewGuid(), "x", "User", "1", new string('a', 3000), Now).Details.Should().HaveLength(2000);
    }
}
