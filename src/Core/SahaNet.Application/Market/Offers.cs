using FluentValidation;
using MediatR;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Domain.Common;
using SahaNet.Domain.Identity;
using SahaNet.Domain.Market;

namespace SahaNet.Application.Market;

public sealed record PlaceOfferCommand(Guid ListingId, decimal UnitPrice, decimal? Quantity, string? Message) : IRequest<Result<OfferResponse>>;

internal sealed class PlaceOfferCommandValidator : AbstractValidator<PlaceOfferCommand>
{
    public PlaceOfferCommandValidator()
    {
        RuleFor(x => x.UnitPrice).GreaterThan(0).LessThanOrEqualTo(100_000_000);
        RuleFor(x => x.Quantity).GreaterThan(0).When(x => x.Quantity is not null);
        RuleFor(x => x.Message).MaximumLength(Offer.MessageMaxLength);
    }
}

internal sealed class PlaceOfferCommandHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    IListingRepository listings,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<PlaceOfferCommand, Result<OfferResponse>>
{
    public async Task<Result<OfferResponse>> Handle(PlaceOfferCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<OfferResponse>(IdentityErrors.Unauthenticated);
        }

        var buyer = await users.GetByIdAsync(userId, cancellationToken);
        if (buyer is null || buyer.Status != UserStatus.Active)
        {
            return Result.Failure<OfferResponse>(IdentityErrors.UserNotFound);
        }

        var listing = await listings.GetByIdAsync(request.ListingId, cancellationToken);
        if (listing is null)
        {
            return Result.Failure<OfferResponse>(MarketErrors.ListingNotFound);
        }

        var offer = listing.PlaceOffer(userId, request.UnitPrice, request.Quantity ?? listing.RemainingQuantity, request.Message, clock.UtcNow);
        if (offer.IsFailure)
        {
            return Result.Failure<OfferResponse>(offer.Error);
        }

        listings.AddOffer(offer.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(offer.Value.ToResponse());
    }
}

public enum OfferDecision
{
    Accept = 0,
    Reject = 1,
    Withdraw = 2,
}

public sealed record DecideOfferCommand(Guid OfferId, OfferDecision Decision) : IRequest<Result>;

internal sealed class DecideOfferCommandHandler(
    ICurrentUser currentUser,
    IListingRepository listings,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<DecideOfferCommand, Result>
{
    public async Task<Result> Handle(DecideOfferCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(IdentityErrors.Unauthenticated);
        }

        var listing = await listings.GetByOfferIdAsync(request.OfferId, cancellationToken);
        if (listing is null)
        {
            return Result.Failure(MarketErrors.OfferNotFound);
        }

        var result = request.Decision switch
        {
            OfferDecision.Accept => listing.AcceptOffer(userId, request.OfferId, clock.UtcNow),
            OfferDecision.Reject => listing.RejectOffer(userId, request.OfferId),
            _ => listing.WithdrawOffer(userId, request.OfferId),
        };

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}
