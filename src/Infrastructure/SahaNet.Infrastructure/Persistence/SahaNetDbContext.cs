using MediatR;
using Microsoft.EntityFrameworkCore;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Events;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Domain.Common;
using SahaNet.Domain.Consents;
using SahaNet.Domain.Events;
using SahaNet.Domain.Identity;
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

    public DbSet<User> Users => Set<User>();

    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<ConsentDocument> ConsentDocuments => Set<ConsentDocument>();

    public DbSet<UserConsent> UserConsents => Set<UserConsent>();

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
