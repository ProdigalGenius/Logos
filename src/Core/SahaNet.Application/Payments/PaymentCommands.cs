using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Application.Market;
using SahaNet.Domain.Common;
using SahaNet.Domain.Identity;
using SahaNet.Domain.Market;
using SahaNet.Domain.Payments;

namespace SahaNet.Application.Payments;

// ---- Ödeme başlat (kabul edilmiş teklif için, idempotent) ----

public sealed record StartPaymentCommand(Guid OfferId) : IRequest<Result<CheckoutResponse>>;

internal sealed class StartPaymentCommandHandler(
    ICurrentUser currentUser,
    IListingRepository listings,
    IPaymentOrderRepository orders,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    IOptions<PaymentOptions> options)
    : IRequestHandler<StartPaymentCommand, Result<CheckoutResponse>>
{
    public async Task<Result<CheckoutResponse>> Handle(StartPaymentCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<CheckoutResponse>(IdentityErrors.Unauthenticated);
        }

        var listing = await listings.GetByOfferIdAsync(request.OfferId, cancellationToken);
        var offer = listing?.Offers.FirstOrDefault(o => o.Id == request.OfferId);
        if (listing is null || offer is null)
        {
            return Result.Failure<CheckoutResponse>(MarketErrors.OfferNotFound);
        }

        if (offer.BuyerId != userId)
        {
            return Result.Failure<CheckoutResponse>(PaymentErrors.NotBuyer);
        }

        var order = await orders.GetOpenByOfferAsync(offer.Id, cancellationToken);
        if (order is null)
        {
            if (offer.Status != OfferStatus.Accepted)
            {
                return Result.Failure<CheckoutResponse>(PaymentErrors.OfferNotAccepted);
            }

            var settings = options.Value;
            order = PaymentOrder.Create(
                listing.Id, offer.Id, userId, listing.SellerId, offer.Total,
                settings.RateFor(listing.CategoryKey), settings.MinimumCommission, settings.PaymentWindow, clock.UtcNow);
            orders.Add(order);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (order.Status != PaymentStatus.AwaitingPayment)
        {
            return Result.Success(new CheckoutResponse(order.ToResponse(userId, false), null));
        }

        var session = await gateway.CreateCheckoutAsync(order, $"{listing.Title} ({offer.Quantity:0.###} {listing.Unit})", cancellationToken);
        return Result.Success(new CheckoutResponse(order.ToResponse(userId, false), session.PaymentUrl));
    }
}

// ---- Sağlayıcı geri çağrısı: ödeme doğrulanır, para emanete alınır ----

public sealed record CompletePaymentCommand(string Token) : IRequest<Result<Guid>>;

internal sealed class CompletePaymentCommandValidator : AbstractValidator<CompletePaymentCommand>
{
    public CompletePaymentCommandValidator() => RuleFor(x => x.Token).NotEmpty().MaximumLength(256);
}

internal sealed class CompletePaymentCommandHandler(
    IPaymentOrderRepository orders,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<CompletePaymentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CompletePaymentCommand request, CancellationToken cancellationToken)
    {
        // Geri çağrıdaki veriye güvenilmez; durum her zaman sağlayıcıdan sorgulanır.
        var verified = await gateway.VerifyAsync(request.Token, cancellationToken);
        if (!verified.Succeeded)
        {
            return Result.Failure<Guid>(PaymentErrors.GatewayFailed);
        }

        var order = await orders.GetByIdAsync(verified.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<Guid>(PaymentErrors.OrderNotFound);
        }

        var result = order.MarkPaid(verified.ProviderPaymentId, verified.Amount, clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(order.Id);
    }
}

// ---- Satıcı: teslim ettim / Alıcı: teslim aldım, itiraz ----

public enum PaymentAction
{
    ReportDelivered = 0,
    ConfirmReceipt = 1,
}

public sealed record PaymentActionCommand(Guid OrderId, PaymentAction Action) : IRequest<Result<PaymentOrderResponse>>;

internal sealed class PaymentActionCommandHandler(
    ICurrentUser currentUser,
    IPaymentOrderRepository orders,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    IOptions<PaymentOptions> options)
    : IRequestHandler<PaymentActionCommand, Result<PaymentOrderResponse>>
{
    public async Task<Result<PaymentOrderResponse>> Handle(PaymentActionCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<PaymentOrderResponse>(IdentityErrors.Unauthenticated);
        }

        var order = await orders.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null || (order.BuyerId != userId && order.SellerId != userId))
        {
            return Result.Failure<PaymentOrderResponse>(PaymentErrors.OrderNotFound);
        }

        var now = clock.UtcNow;
        var result = request.Action == PaymentAction.ReportDelivered
            ? order.ReportDelivered(userId, options.Value.DisputeWindow, now)
            : order.ConfirmReceipt(userId, now);
        if (result.IsFailure)
        {
            return Result.Failure<PaymentOrderResponse>(result.Error);
        }

        // Sağlayıcı aktarımı reddederse (istisna) durum kaydedilmez; işlem tekrar denenebilir.
        if (order.Status == PaymentStatus.Released)
        {
            await gateway.ReleaseAsync(order, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(order.ToResponse(userId, currentUser.IsAdmin));
    }
}

public sealed record OpenDisputeCommand(Guid OrderId, string Reason) : IRequest<Result<PaymentOrderResponse>>;

internal sealed class OpenDisputeCommandValidator : AbstractValidator<OpenDisputeCommand>
{
    public OpenDisputeCommandValidator() => RuleFor(x => x.Reason).NotEmpty().MinimumLength(5).MaximumLength(PaymentOrder.ReasonMaxLength);
}

internal sealed class OpenDisputeCommandHandler(ICurrentUser currentUser, IPaymentOrderRepository orders, IUnitOfWork unitOfWork, IDateTimeProvider clock)
    : IRequestHandler<OpenDisputeCommand, Result<PaymentOrderResponse>>
{
    public async Task<Result<PaymentOrderResponse>> Handle(OpenDisputeCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<PaymentOrderResponse>(IdentityErrors.Unauthenticated);
        }

        var order = await orders.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null || (order.BuyerId != userId && order.SellerId != userId))
        {
            return Result.Failure<PaymentOrderResponse>(PaymentErrors.OrderNotFound);
        }

        var result = order.OpenDispute(userId, request.Reason, clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<PaymentOrderResponse>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(order.ToResponse(userId, false));
    }
}

// ---- Yönetici: itiraz kararı ----

public sealed record ResolveDisputeCommand(Guid OrderId, string Decision, string? Note) : IRequest<Result<PaymentOrderResponse>>;

internal sealed class ResolveDisputeCommandValidator : AbstractValidator<ResolveDisputeCommand>
{
    public ResolveDisputeCommandValidator()
    {
        RuleFor(x => x.Decision).Must(d => Enum.TryParse<DisputeDecision>(d, true, out _)).WithMessage("Karar ReleaseToSeller veya RefundBuyer olmalı.");
        RuleFor(x => x.Note).MaximumLength(PaymentOrder.ReasonMaxLength);
    }
}

internal sealed class ResolveDisputeCommandHandler(
    ICurrentUser currentUser,
    IPaymentOrderRepository orders,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<ResolveDisputeCommand, Result<PaymentOrderResponse>>
{
    public static readonly Error AdminOnly = new("payment.admin_only", "Bu işlem yöneticiye açık.", ErrorType.Forbidden);

    public async Task<Result<PaymentOrderResponse>> Handle(ResolveDisputeCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } adminId || !currentUser.IsAdmin)
        {
            return Result.Failure<PaymentOrderResponse>(AdminOnly);
        }

        var order = await orders.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<PaymentOrderResponse>(PaymentErrors.OrderNotFound);
        }

        var decision = Enum.Parse<DisputeDecision>(request.Decision, true);
        var result = order.Resolve(adminId, decision, request.Note, clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<PaymentOrderResponse>(result.Error);
        }

        if (decision == DisputeDecision.ReleaseToSeller)
        {
            await gateway.ReleaseAsync(order, cancellationToken);
        }
        else
        {
            await gateway.RefundAsync(order, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(order.ToResponse(adminId, true));
    }
}

// ---- Bakım işi: ödenmeyenleri iptal, itiraz süresi dolanları serbest bırak ----

public sealed record PaymentMaintenanceResult(int Cancelled, int Released, int Failed);

public sealed record RunPaymentMaintenanceCommand : IRequest<PaymentMaintenanceResult>;

internal sealed class RunPaymentMaintenanceCommandHandler(
    IPaymentOrderRepository orders,
    IListingRepository listings,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<RunPaymentMaintenanceCommand, PaymentMaintenanceResult>
{
    public async Task<PaymentMaintenanceResult> Handle(RunPaymentMaintenanceCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        int cancelled = 0, released = 0, failed = 0;

        foreach (var order in await orders.GetDueForMaintenanceAsync(now, 100, cancellationToken))
        {
            if (order.CancelIfUnpaid(now))
            {
                var listing = await listings.GetByIdAsync(order.ListingId, cancellationToken);
                listing?.CancelAcceptedOffer(order.OfferId, now);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                cancelled++;
            }
            else if (order.AutoReleaseIfDue(now))
            {
                try
                {
                    await gateway.ReleaseAsync(order, cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    released++;
                }
                catch (HttpRequestException)
                {
                    // Bir sonraki çalışmada tekrar denenir (durum kaydedilmedi).
                    failed++;
                }
            }
        }

        return new PaymentMaintenanceResult(cancelled, released, failed);
    }
}

// ---- Sorgular ----

public sealed record GetMyPaymentsQuery : IRequest<Result<IReadOnlyList<PaymentOrderResponse>>>;

internal sealed class GetMyPaymentsQueryHandler(ICurrentUser currentUser, IPaymentOrderRepository orders)
    : IRequestHandler<GetMyPaymentsQuery, Result<IReadOnlyList<PaymentOrderResponse>>>
{
    public async Task<Result<IReadOnlyList<PaymentOrderResponse>>> Handle(GetMyPaymentsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<IReadOnlyList<PaymentOrderResponse>>(IdentityErrors.Unauthenticated);
        }

        var mine = await orders.GetForUserAsync(userId, cancellationToken);
        return Result.Success<IReadOnlyList<PaymentOrderResponse>>(mine.Select(o => o.ToResponse(userId, false)).ToList());
    }
}

public sealed record GetDisputesQuery : IRequest<Result<IReadOnlyList<PaymentOrderResponse>>>;

internal sealed class GetDisputesQueryHandler(ICurrentUser currentUser, IPaymentOrderRepository orders)
    : IRequestHandler<GetDisputesQuery, Result<IReadOnlyList<PaymentOrderResponse>>>
{
    public async Task<Result<IReadOnlyList<PaymentOrderResponse>>> Handle(GetDisputesQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } adminId || !currentUser.IsAdmin)
        {
            return Result.Failure<IReadOnlyList<PaymentOrderResponse>>(ResolveDisputeCommandHandler.AdminOnly);
        }

        var disputed = await orders.GetDisputedAsync(cancellationToken);
        return Result.Success<IReadOnlyList<PaymentOrderResponse>>(disputed.Select(o => o.ToResponse(adminId, true)).ToList());
    }
}

public sealed record GetCommissionSummaryQuery(DateTimeOffset From, DateTimeOffset To) : IRequest<Result<IReadOnlyList<CommissionSummaryRow>>>;

internal sealed class GetCommissionSummaryQueryHandler(ICurrentUser currentUser, IPaymentOrderRepository orders)
    : IRequestHandler<GetCommissionSummaryQuery, Result<IReadOnlyList<CommissionSummaryRow>>>
{
    public async Task<Result<IReadOnlyList<CommissionSummaryRow>>> Handle(GetCommissionSummaryQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAdmin)
        {
            return Result.Failure<IReadOnlyList<CommissionSummaryRow>>(ResolveDisputeCommandHandler.AdminOnly);
        }

        return Result.Success(await orders.GetCommissionSummaryAsync(request.From.ToUniversalTime(), request.To.ToUniversalTime(), cancellationToken));
    }
}
