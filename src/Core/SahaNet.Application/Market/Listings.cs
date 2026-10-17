using System.Globalization;
using FluentValidation;
using MediatR;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Common;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Domain.Common;
using SahaNet.Domain.Identity;
using SahaNet.Domain.Market;
using SahaNet.Application.Moderation;

namespace SahaNet.Application.Market;

public sealed record OfferResponse(
    Guid Id,
    Guid ListingId,
    Guid BuyerId,
    decimal UnitPrice,
    decimal Quantity,
    decimal Total,
    string? Message,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record ListingResponse(
    Guid Id,
    Guid SellerId,
    string? SellerName,
    string NicheKey,
    string CategoryKey,
    string Title,
    string? Description,
    string Type,
    string Status,
    decimal UnitPrice,
    decimal Quantity,
    decimal RemainingQuantity,
    string Unit,
    string? RegionCode,
    IReadOnlyDictionary<string, string> Attributes,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    decimal? HighestBid,
    decimal? NextMinimumBid,
    int OfferCount,
    IReadOnlyList<OfferResponse> Offers,
    string ShareUrl);

public sealed record ListingPage(IReadOnlyList<ListingResponse> Items, int Page, int PageSize, int Total);

internal static class ListingMapping
{
    public static OfferResponse ToResponse(this Offer o) =>
        new(o.Id, o.ListingId, o.BuyerId, o.UnitPrice, o.Quantity, o.Total, o.Message, o.Status.ToString(), o.CreatedAt);

    /// <summary>Teklif ayrıntıları gizlidir: satıcı tümünü, alıcı yalnızca kendisininkini görür.</summary>
    public static ListingResponse ToResponse(this Listing l, Guid? viewerId, string? sellerName, string publicBaseUrl)
    {
        var visibleOffers = l.Offers
            .Where(o => viewerId is { } v && (v == l.SellerId || v == o.BuyerId))
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => o.ToResponse())
            .ToList();
        var highest = l.Type == ListingType.Auction ? l.HighestBid?.UnitPrice : null;
        var nextMin = l.Type == ListingType.Auction && l.Status == ListingStatus.Active
            ? (highest is { } h ? h + l.MinBidIncrement!.Value : l.UnitPrice)
            : (decimal?)null;

        return new ListingResponse(
            l.Id, l.SellerId, sellerName, l.NicheKey, l.CategoryKey, l.Title, l.Description,
            l.Type.ToString(), l.Status.ToString(), l.UnitPrice, l.Quantity, l.RemainingQuantity, l.Unit, l.RegionCode,
            l.Attributes, l.CreatedAt, l.ExpiresAt, highest, nextMin, l.Offers.Count, visibleOffers,
            WhatsAppShare.Link(l, publicBaseUrl));
    }
}

/// <summary>WhatsApp paylaşım linki: hedef kitlenin en yaygın kanalı.</summary>
public static class WhatsAppShare
{
    public static string Link(Listing listing, string publicBaseUrl)
    {
        ArgumentNullException.ThrowIfNull(listing);
        var price = listing.UnitPrice.ToString("N2", CultureInfo.GetCultureInfo("tr-TR"));
        var text = $"{listing.Title} — {price} TL/{listing.Unit} {publicBaseUrl.TrimEnd('/')}/l/{listing.Id:N}";
        return "https://wa.me/?text=" + Uri.EscapeDataString(text);
    }
}

public sealed class MarketOptions
{
    public const string SectionName = "Market";

    /// <summary>Paylaşım linklerinde kullanılan genel adres (niş markasına göre değişir).</summary>
    public string PublicBaseUrl { get; init; } = "https://balikcinet.app";

    public TimeSpan MaxListingDuration { get; init; } = TimeSpan.FromDays(30);

    public int MaxMessagesPerMinute { get; init; } = 20;
}

// ---- Kategoriler ----

public sealed record CategoryResponse(string Key, string NicheKey, string Name, string DefaultUnit, IReadOnlyList<string> AllowedTypes, IReadOnlyList<AttributeDefinition> Attributes);

public sealed record GetCategoriesQuery(string? NicheKey) : IRequest<IReadOnlyList<CategoryResponse>>;

internal sealed class GetCategoriesQueryHandler(IEnumerable<IListingCategoryProvider> providers)
    : IRequestHandler<GetCategoriesQuery, IReadOnlyList<CategoryResponse>>
{
    public Task<IReadOnlyList<CategoryResponse>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<CategoryResponse> result = providers
            .SelectMany(p => p.GetCategories())
            .Where(c => request.NicheKey is null || c.NicheKey == request.NicheKey)
            .Select(c => new CategoryResponse(c.Key, c.NicheKey, c.Name, c.DefaultUnit, c.AllowedTypes.Select(t => t.ToString()).ToList(), c.Attributes))
            .ToList();
        return Task.FromResult(result);
    }
}

// ---- İlan ver ----

public sealed record CreateListingCommand(
    string CategoryKey,
    string Title,
    string? Description,
    string Type,
    decimal UnitPrice,
    decimal Quantity,
    string? Unit,
    string? RegionCode,
    IReadOnlyDictionary<string, string>? Attributes,
    DateTimeOffset? ExpiresAt,
    decimal? MinBidIncrement,
    decimal? ReservePrice) : IRequest<Result<ListingResponse>>;

internal sealed class CreateListingCommandValidator : AbstractValidator<CreateListingCommand>
{
    public CreateListingCommandValidator()
    {
        RuleFor(x => x.CategoryKey).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(Listing.TitleMaxLength);
        RuleFor(x => x.Description).MaximumLength(Listing.DescriptionMaxLength);
        RuleFor(x => x.Type).Must(t => Enum.TryParse<ListingType>(t, ignoreCase: true, out _)).WithMessage("Tür FixedPrice, OfferBased veya Auction olmalı.");
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100_000_000);
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(1_000_000);
        RuleFor(x => x.Unit).MaximumLength(16);
        RuleFor(x => x.MinBidIncrement).GreaterThan(0).When(x => x.MinBidIncrement is not null);
        RuleFor(x => x.ReservePrice).GreaterThanOrEqualTo(0).When(x => x.ReservePrice is not null);
    }
}

internal sealed class CreateListingCommandHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    IListingRepository listings,
    IEnumerable<IListingCategoryProvider> providers,
    ISpamFilter spamFilter,
    IModerationService moderation,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    Microsoft.Extensions.Options.IOptions<MarketOptions> options)
    : IRequestHandler<CreateListingCommand, Result<ListingResponse>>
{
    public async Task<Result<ListingResponse>> Handle(CreateListingCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<ListingResponse>(IdentityErrors.Unauthenticated);
        }

        var seller = await users.GetByIdAsync(userId, cancellationToken);
        if (seller is null || !seller.IsOnboarded || seller.Status != UserStatus.Active)
        {
            return Result.Failure<ListingResponse>(MarketErrors.OnboardingRequired);
        }

        var category = providers.SelectMany(p => p.GetCategories()).FirstOrDefault(c => c.Key == request.CategoryKey);
        if (category is null)
        {
            return Result.Failure<ListingResponse>(MarketErrors.CategoryUnknown);
        }

        var type = Enum.Parse<ListingType>(request.Type, ignoreCase: true);
        if (!category.AllowedTypes.Contains(type))
        {
            return Result.Failure<ListingResponse>(MarketErrors.AttributeInvalid($"Bu kategoride {type} ilan verilemez."));
        }

        if (request.RegionCode is not null && RegionCatalog.Find(request.RegionCode) is null)
        {
            return Result.Failure<ListingResponse>(IdentityErrors.RegionUnknown);
        }

        var attributes = AttributeValidator.Validate(category, request.Attributes);
        if (attributes.IsFailure)
        {
            return Result.Failure<ListingResponse>(attributes.Error);
        }

        var recentTitles = (await listings.GetBySellerAsync(userId, cancellationToken))
            .Where(l => l.CreatedAt > clock.UtcNow.AddDays(-1))
            .Select(l => l.Title)
            .ToList();
        var spam = spamFilter.Evaluate(SpamContent.Listing, $"{request.Title}\n{request.Description}", recentTitles);
        if (spam.Verdict == SpamVerdict.Block)
        {
            return Result.Failure<ListingResponse>(SahaNet.Domain.Moderation.ModerationErrors.SpamBlocked);
        }

        var now = clock.UtcNow;
        var maxEnd = now + options.Value.MaxListingDuration;
        var expiresAt = request.ExpiresAt is { } e ? (e > maxEnd ? maxEnd : e) : now + TimeSpan.FromDays(type == ListingType.Auction ? 1 : 7);
        if (expiresAt <= now.AddMinutes(5))
        {
            return Result.Failure<ListingResponse>(MarketErrors.AttributeInvalid("Bitiş zamanı en az 5 dakika sonra olmalı."));
        }

        var listing = Listing.Create(
            userId, category.NicheKey, category.Key, request.Title, request.Description, type,
            request.UnitPrice, request.Quantity, string.IsNullOrWhiteSpace(request.Unit) ? category.DefaultUnit : request.Unit.Trim(),
            request.RegionCode ?? seller.RegionCode, attributes.Value, expiresAt, request.MinBidIncrement, request.ReservePrice, now);

        listings.Add(listing);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (spam.Verdict == SpamVerdict.Review)
        {
            // Yayında kalır, moderatör kuyruğuna düşer.
            await moderation.FlagAsync(SahaNet.Domain.Moderation.ReportTargetType.Listing, listing.Id, userId, spam, cancellationToken);
        }

        return Result.Success(listing.ToResponse(userId, seller.DisplayName, options.Value.PublicBaseUrl));
    }
}

// ---- Arama / detay ----

public sealed record SearchListingsQuery(
    string? NicheKey,
    string? CategoryKey,
    string? RegionCode,
    string? Type,
    string? Text,
    int Page = 1,
    int PageSize = 20) : IRequest<ListingPage>;

internal sealed class SearchListingsQueryValidator : AbstractValidator<SearchListingsQuery>
{
    public SearchListingsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.Text).MaximumLength(100);
        RuleFor(x => x.Type).Must(t => t is null || Enum.TryParse<ListingType>(t, true, out _));
    }
}

internal sealed class SearchListingsQueryHandler(
    IListingRepository listings,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    Microsoft.Extensions.Options.IOptions<MarketOptions> options)
    : IRequestHandler<SearchListingsQuery, ListingPage>
{
    public async Task<ListingPage> Handle(SearchListingsQuery request, CancellationToken cancellationToken)
    {
        var type = request.Type is null ? (ListingType?)null : Enum.Parse<ListingType>(request.Type, true);
        var (items, total) = await listings.SearchAsync(
            new ListingSearch(request.NicheKey, request.CategoryKey, request.RegionCode, type, request.Text, request.Page, request.PageSize),
            clock.UtcNow,
            cancellationToken);

        return new ListingPage(
            items.Select(l => l.ToResponse(currentUser.UserId, null, options.Value.PublicBaseUrl)).ToList(),
            request.Page,
            request.PageSize,
            total);
    }
}

public sealed record GetListingQuery(Guid Id) : IRequest<Result<ListingResponse>>;

internal sealed class GetListingQueryHandler(
    IListingRepository listings,
    IUserRepository users,
    ICurrentUser currentUser,
    Microsoft.Extensions.Options.IOptions<MarketOptions> options)
    : IRequestHandler<GetListingQuery, Result<ListingResponse>>
{
    public async Task<Result<ListingResponse>> Handle(GetListingQuery request, CancellationToken cancellationToken)
    {
        var listing = await listings.GetByIdAsync(request.Id, cancellationToken);
        if (listing is null)
        {
            return Result.Failure<ListingResponse>(MarketErrors.ListingNotFound);
        }

        var seller = await users.GetByIdAsync(listing.SellerId, cancellationToken);
        return Result.Success(listing.ToResponse(currentUser.UserId, seller?.DisplayName, options.Value.PublicBaseUrl));
    }
}

// ---- İlanlarım / tekliflerim ----

public sealed record GetMyListingsQuery : IRequest<Result<IReadOnlyList<ListingResponse>>>;

internal sealed class GetMyListingsQueryHandler(
    IListingRepository listings,
    ICurrentUser currentUser,
    Microsoft.Extensions.Options.IOptions<MarketOptions> options)
    : IRequestHandler<GetMyListingsQuery, Result<IReadOnlyList<ListingResponse>>>
{
    public async Task<Result<IReadOnlyList<ListingResponse>>> Handle(GetMyListingsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<IReadOnlyList<ListingResponse>>(IdentityErrors.Unauthenticated);
        }

        var mine = await listings.GetBySellerAsync(userId, cancellationToken);
        return Result.Success<IReadOnlyList<ListingResponse>>(mine.Select(l => l.ToResponse(userId, null, options.Value.PublicBaseUrl)).ToList());
    }
}

public sealed record GetMyOffersQuery : IRequest<Result<IReadOnlyList<ListingResponse>>>;

internal sealed class GetMyOffersQueryHandler(
    IListingRepository listings,
    ICurrentUser currentUser,
    Microsoft.Extensions.Options.IOptions<MarketOptions> options)
    : IRequestHandler<GetMyOffersQuery, Result<IReadOnlyList<ListingResponse>>>
{
    public async Task<Result<IReadOnlyList<ListingResponse>>> Handle(GetMyOffersQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<IReadOnlyList<ListingResponse>>(IdentityErrors.Unauthenticated);
        }

        var withMyOffers = await listings.GetWithOffersByBuyerAsync(userId, cancellationToken);
        return Result.Success<IReadOnlyList<ListingResponse>>(withMyOffers.Select(l => l.ToResponse(userId, null, options.Value.PublicBaseUrl)).ToList());
    }
}

// ---- İlanı kapat ----

public sealed record CancelListingCommand(Guid Id) : IRequest<Result>;

internal sealed class CancelListingCommandHandler(IListingRepository listings, ICurrentUser currentUser, IUnitOfWork unitOfWork)
    : IRequestHandler<CancelListingCommand, Result>
{
    public async Task<Result> Handle(CancelListingCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(IdentityErrors.Unauthenticated);
        }

        var listing = await listings.GetByIdAsync(request.Id, cancellationToken);
        if (listing is null)
        {
            return Result.Failure(MarketErrors.ListingNotFound);
        }

        var result = listing.Cancel(userId);
        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}

// ---- Süresi dolan ilan/ihaleleri kapat (Hangfire, dakikalık) ----

public sealed record CloseDueListingsCommand : IRequest<int>;

internal sealed class CloseDueListingsCommandHandler(IListingRepository listings, IUnitOfWork unitOfWork, IDateTimeProvider clock)
    : IRequestHandler<CloseDueListingsCommand, int>
{
    private const int BatchSize = 100;

    public async Task<int> Handle(CloseDueListingsCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var closed = 0;
        foreach (var listing in await listings.GetDueForClosingAsync(now, BatchSize, cancellationToken))
        {
            if (listing.CloseIfDue(now))
            {
                closed++;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return closed;
    }
}
