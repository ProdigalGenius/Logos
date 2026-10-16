using SahaNet.Application.Events;
using SahaNet.Domain.Events;

namespace SahaNet.UnitTests.Application;

public class DomainEventNotificationTests
{
    public sealed record StormWarningIssued(string Region) : DomainEvent;

    [Fact]
    public void Create_WrapsEventWithRuntimeType()
    {
        var evt = new StormWarningIssued("Karadeniz");

        var notification = DomainEventNotification.Create(evt);

        notification.Should().BeOfType<DomainEventNotification<StormWarningIssued>>()
            .Which.DomainEvent.Should().Be(evt);
    }
}
