using MediatR;
using Microsoft.EntityFrameworkCore;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Events;
using SahaNet.Domain.Common;
using SahaNet.Domain.Events;

namespace SahaNet.Infrastructure.Persistence;

/// <summary>
/// Tek PostgreSQL veritabanı; her modül kendi şemasında (örn. "fishing") tablolarını tutar.
/// </summary>
public sealed class SahaNetDbContext(DbContextOptions<SahaNetDbContext> options, IPublisher publisher)
    : DbContext(options), IUnitOfWork
{
    public const string CoreSchema = "core";

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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SahaNetDbContext).Assembly);

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
