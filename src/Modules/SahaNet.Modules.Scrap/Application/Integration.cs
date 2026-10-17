using MediatR;
using Microsoft.EntityFrameworkCore;
using SahaNet.Application.Events;
using SahaNet.Application.Market;
using SahaNet.Application.Moderation;
using SahaNet.Application.Modules;
using SahaNet.Domain.Identity;
using SahaNet.Domain.Market;
using SahaNet.Modules.Scrap.Domain;
using SahaNet.Modules.Scrap.Persistence;

namespace SahaNet.Modules.Scrap.Application;

/// <summary>Çekirdek pazarı hurda kategorileriyle genişletir.</summary>
internal sealed class ScrapListingCategories : IListingCategoryProvider
{
    private static readonly IReadOnlyList<string> MaterialCodes = ScrapSeed.Materials.Select(m => m.Code).ToList();
    private static readonly IReadOnlyList<string> ProvinceCodes = SahaNet.Application.Common.RegionCatalog.All.Select(r => r.Code).ToList();

    private static readonly ListingCategory[] Categories =
    [
        // Fabrika/atölye çıkışı toplu hurda: hurdacılar teklif verir veya ihaleye girer.
        new(
            "scrap-lot",
            ScrapModuleDescriptor.ModuleKey,
            "Toplu hurda",
            "kg",
            [ListingType.OfferBased, ListingType.Auction, ListingType.FixedPrice],
            [
                new AttributeDefinition("material", "Malzeme", AttributeType.Choice, Required: true, Choices: MaterialCodes),
                new AttributeDefinition("province", "İl", AttributeType.Choice, Required: true, Choices: ProvinceCodes),
                new AttributeDefinition("district", "İlçe", AttributeType.Text, Required: true, MaxLength: 64),
                new AttributeDefinition("loadingHelp", "Yükleme desteği var", AttributeType.Choice, Required: false, Choices: ["evet", "hayir"]),
            ]),
        new(
            "scrap-equipment",
            ScrapModuleDescriptor.ModuleKey,
            "Ekipman (pres, kantar, araç)",
            "adet",
            [ListingType.FixedPrice, ListingType.OfferBased],
            [
                new AttributeDefinition("kind", "Ekipman türü", AttributeType.Choice, Required: true, Choices: ["kantar", "pres", "kirici", "arac", "diger"]),
                new AttributeDefinition("condition", "Durum", AttributeType.Choice, Required: true, Choices: ["sifir", "kullanilmis", "tamir-gerekli"]),
            ]),
    ];

    public IEnumerable<ListingCategory> GetCategories() => Categories;
}

internal sealed class ScrapPersonalDataSection(ScrapDbContext db) : IPersonalDataSection
{
    public string Name => ScrapModuleDescriptor.ModuleKey;

    public async Task<object?> ExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        var pickups = await db.Pickups.AsNoTracking().Where(p => p.RequesterId == userId || p.CollectorId == userId).ToListAsync(cancellationToken);
        var prices = await db.Prices.AsNoTracking().Where(p => p.ReportedBy == userId).ToListAsync(cancellationToken);
        return new
        {
            Pickups = pickups.Select(p => new
            {
                Role = p.RequesterId == userId ? "Requester" : "Collector",
                p.ProvinceCode,
                p.District,
                Address = p.CanSeeAddress(userId) ? p.Address : null,
                p.Materials,
                p.EstimatedKg,
                Status = p.Status.ToString(),
                p.CreatedAt,
            }),
            PriceReports = prices.Select(p => new { p.MaterialCode, p.ProvinceCode, p.Date, p.PricePerKg }),
        };
    }
}

/// <summary>Hesap kalıcı silinince: kendi talepleri silinir, üstlendiği işlerden adı düşer, fiyat bildirimleri silinir.</summary>
internal sealed class DeleteScrapDataOnUserPurged(ScrapDbContext db) : INotificationHandler<DomainEventNotification<UserPurged>>
{
    public async Task Handle(DomainEventNotification<UserPurged> notification, CancellationToken cancellationToken)
    {
        var userId = notification.DomainEvent.UserId;
        await db.Pickups.Where(p => p.RequesterId == userId).ExecuteDeleteAsync(cancellationToken);
        await db.Prices.Where(p => p.ReportedBy == userId).ExecuteDeleteAsync(cancellationToken);
        foreach (var job in await db.Pickups.Where(p => p.CollectorId == userId).ToListAsync(cancellationToken))
        {
            if (job.Status == PickupStatus.Claimed)
            {
                job.Release(userId);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class ScrapAdminMetrics(ScrapDbContext db) : IAdminMetricsSection
{
    public string Name => ScrapModuleDescriptor.ModuleKey;

    public async Task<IReadOnlyDictionary<string, decimal>> GetAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var d30 = now.AddDays(-30);
        return new Dictionary<string, decimal>
        {
            ["openPickups"] = await db.Pickups.CountAsync(p => p.Status == PickupStatus.Open, cancellationToken),
            ["completedPickups30Days"] = await db.Pickups.CountAsync(p => p.Status == PickupStatus.Completed && p.ClosedAt >= d30, cancellationToken),
            ["collectedKg30Days"] = await db.Pickups.Where(p => p.Status == PickupStatus.Completed && p.ClosedAt >= d30).SumAsync(p => p.EstimatedKg, cancellationToken),
            ["priceReports30Days"] = await db.Prices.CountAsync(p => p.CreatedAt >= d30, cancellationToken),
        };
    }
}
