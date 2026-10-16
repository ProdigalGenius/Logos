using MediatR;
using Microsoft.EntityFrameworkCore;
using SahaNet.Application.Events;
using SahaNet.Application.Modules;
using SahaNet.Domain.Consents;
using SahaNet.Domain.Identity;
using SahaNet.Modules.Fishing.Persistence;

namespace SahaNet.Modules.Fishing.Application;

/// <summary>KVKK "verilerimi indir": av noktaları (koordinatlar dahil, kullanıcının kendi verisi) ve av günlüğü.</summary>
internal sealed class FishingPersonalDataSection(FishingDbContext db) : IPersonalDataSection
{
    public string Name => FishingModuleDescriptor.ModuleKey;

    public async Task<object?> ExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        var spots = await db.Spots.AsNoTracking().Where(s => s.OwnerId == userId).ToListAsync(cancellationToken);
        var catches = await db.Catches.AsNoTracking().Where(c => c.UserId == userId).OrderBy(c => c.CaughtAt).ToListAsync(cancellationToken);

        return new
        {
            Spots = spots.Select(s => new
            {
                s.Name,
                s.Location.Latitude,
                s.Location.Longitude,
                s.DepthM,
                s.Notes,
                Species = s.SpeciesList,
                SharedWithCount = s.Shares.Count,
                s.CreatedAt,
            }),
            Catches = catches.Select(c => new { c.CaughtAt, c.SpeciesCode, c.WeightKg, c.Count, c.LengthCm, c.Notes }),
        };
    }
}

/// <summary>Hesap kalıcı silinince modüldeki tüm kişisel veriyi siler.</summary>
internal sealed class DeleteFishingDataOnUserPurged(FishingDbContext db) : INotificationHandler<DomainEventNotification<UserPurged>>
{
    public async Task Handle(DomainEventNotification<UserPurged> notification, CancellationToken cancellationToken)
    {
        var userId = notification.DomainEvent.UserId;

        await db.Catches.Where(c => c.UserId == userId).ExecuteDeleteAsync(cancellationToken);

        // Başkalarının bu kullanıcıyla yaptığı paylaşımlar ve kullanıcının kendi noktaları.
        var spots = await db.Spots.Where(s => s.OwnerId == userId || s.Shares.Any(x => x.UserId == userId)).ToListAsync(cancellationToken);
        foreach (var spot in spots)
        {
            if (spot.OwnerId == userId)
            {
                db.Spots.Remove(spot);
            }
            else
            {
                spot.Unshare(userId);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Konum paylaşımı rızası geri çekilince kullanıcının tüm paylaşımlarını kaldırır.</summary>
internal sealed class UnshareSpotsOnLocationConsentWithdrawn(FishingDbContext db)
    : INotificationHandler<DomainEventNotification<LocationSharingWithdrawn>>
{
    public async Task Handle(DomainEventNotification<LocationSharingWithdrawn> notification, CancellationToken cancellationToken)
    {
        var userId = notification.DomainEvent.UserId;
        var spots = await db.Spots.Where(s => s.OwnerId == userId && s.Shares.Any()).ToListAsync(cancellationToken);
        spots.ForEach(s => s.UnshareAll());
        await db.SaveChangesAsync(cancellationToken);
    }
}
