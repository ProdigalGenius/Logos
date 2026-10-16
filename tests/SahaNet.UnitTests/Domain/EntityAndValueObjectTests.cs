using SahaNet.Domain.Common;
using SahaNet.Domain.Events;

namespace SahaNet.UnitTests.Domain;

public class EntityAndValueObjectTests
{
    private sealed class Boat(Guid id) : AggregateRoot<Guid>(id)
    {
        public void Launch() => Raise(new BoatLaunched(Id));
    }

    private sealed record BoatLaunched(Guid BoatId) : DomainEvent;

    private sealed class Coordinate(double lat, double lon) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return lat;
            yield return lon;
        }
    }

    [Fact]
    public void Entities_WithSameId_AreEqual()
    {
        var id = Guid.NewGuid();

        (new Boat(id) == new Boat(id)).Should().BeTrue();
        new Boat(id).Should().NotBe(new Boat(Guid.NewGuid()));
    }

    [Fact]
    public void ValueObjects_WithSameComponents_AreEqual()
    {
        (new Coordinate(41.0, 29.0) == new Coordinate(41.0, 29.0)).Should().BeTrue();
        new Coordinate(41.0, 29.0).GetHashCode().Should().Be(new Coordinate(41.0, 29.0).GetHashCode());
        (new Coordinate(41.0, 29.0) != new Coordinate(40.0, 29.0)).Should().BeTrue();
    }

    [Fact]
    public void AggregateRoot_CollectsAndClearsDomainEvents()
    {
        var boat = new Boat(Guid.NewGuid());

        boat.Launch();

        boat.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<BoatLaunched>();
        boat.ClearDomainEvents();
        boat.DomainEvents.Should().BeEmpty();
    }
}
