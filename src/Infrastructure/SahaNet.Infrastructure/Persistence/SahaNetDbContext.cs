using MediatR;
using Microsoft.EntityFrameworkCore;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Events;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Domain.Alerts;
using SahaNet.Domain.Common;
using SahaNet.Domain.Consents;
using SahaNet.Domain.Events;
using SahaNet.Domain.Geo;
using SahaNet.Domain.Identity;
using SahaNet.Domain.Market;
using SahaNet.Domain.Notifications;
using SahaNet.Domain.Payments;
using SahaNet.Infrastructure.Persistence.Configurations;

namespace SahaNet.Infrastructure.Persistence;

/// <summary>
/// Tek PostgreSQL veritabanı; her bağlam kendi şemasında (core, identity, fishing ...) tablolarını tutar.
/// </summary>
public sealed class SahaNetDbContext(
    DbContextOptions<SahaNetDbContext> options,
    IPublisher publisher,
    IPersonalDataProtector protector)
    : DbContext(options), IUnitOfWork
{
    public const string CoreSchema = "core";
    public const string IdentitySchema = "identity";
    public const string AlertsSchema = "alerts";
    public const string MarketSchema = "market";
    public const string PaymentsSchema = "payments";

    public DbSet<User> Users => Set<User>();

    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<ConsentDocument> ConsentDocuments => Set<ConsentDocument>();

    public DbSet<UserConsent> UserConsents => Set<UserConsent>();

    public DbSet<Region> Regions => Set<Region>();

    public DbSet<RegionSubscription> RegionSubscriptions => Set<RegionSubscription>();

    public DbSet<DeviceRegistration> Devices => Set<DeviceRegistration>();

    public DbSet<Alert> Alerts => Set<Alert>();

    public DbSet<AlertDelivery> AlertDeliveries => Set<AlertDelivery>();

    public DbSet<Listing> Listings => Set<Listing>();

    public DbSet<Offer> Offers => Set<Offer>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<ChatMessage> Messages => Set<ChatMessage>();

    public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var events = CollectDomainEvents();
        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in events)
        {
            await publisher.Publish(DomainEventNotification.Create(domainEvent), cancellationToken);
        }

        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(CoreSchema);
        modelBuilder.HasPostgresExtension("postgis");

        modelBuilder.ApplyConfiguration(new UserConfiguration(protector));
        modelBuilder.ApplyConfiguration(new OtpChallengeConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
        modelBuilder.ApplyConfiguration(new ConsentDocumentConfiguration());
        modelBuilder.ApplyConfiguration(new UserConsentConfiguration());
        modelBuilder.ApplyConfiguration(new RegionConfiguration());
        modelBuilder.ApplyConfiguration(new RegionSubscriptionConfiguration());
        modelBuilder.ApplyConfiguration(new DeviceRegistrationConfiguration());
        modelBuilder.ApplyConfiguration(new AlertConfiguration());
        modelBuilder.ApplyConfiguration(new AlertDeliveryConfiguration());
        modelBuilder.ApplyConfiguration(new ListingConfiguration());
        modelBuilder.ApplyConfiguration(new OfferConfiguration());
        modelBuilder.ApplyConfiguration(new ConversationConfiguration());
        modelBuilder.ApplyConfiguration(new ChatMessageConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentOrderConfiguration());

        base.OnModelCreating(modelBuilder);
    }

    private List<IDomainEvent> CollectDomainEvents()
    {
        var aggregates = ChangeTracker.Entries()
            .Select(e => e.Entity)
            .OfType<IHasDomainEvents>()
            .ToList();

        var events = aggregates.SelectMany(a => a.DomainEvents).ToList();
        aggregates.ForEach(a => a.ClearDomainEvents());
        return events;
    }
}
