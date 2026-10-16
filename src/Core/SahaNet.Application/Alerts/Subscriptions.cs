using FluentValidation;
using MediatR;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Domain.Common;
using SahaNet.Domain.Geo;
using SahaNet.Domain.Identity;
using SahaNet.Domain.Notifications;

namespace SahaNet.Application.Alerts;

public sealed record SubscriptionResponse(Guid RegionId, string RegionCode, string RegionName, IReadOnlyList<string> Topics, DateTimeOffset CreatedAt);

public sealed record GetMySubscriptionsQuery : IRequest<Result<IReadOnlyList<SubscriptionResponse>>>;

internal sealed class GetMySubscriptionsQueryHandler(ICurrentUser currentUser, ISubscriptionRepository subscriptions, IRegionRepository regions)
    : IRequestHandler<GetMySubscriptionsQuery, Result<IReadOnlyList<SubscriptionResponse>>>
{
    public async Task<Result<IReadOnlyList<SubscriptionResponse>>> Handle(GetMySubscriptionsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<IReadOnlyList<SubscriptionResponse>>(IdentityErrors.Unauthenticated);
        }

        return Result.Success(await SubscriptionViews.BuildAsync(userId, subscriptions, regions, cancellationToken));
    }
}

/// <summary>Bölgeye abone ol ya da konuları güncelle (idempotent).</summary>
public sealed record SubscribeToRegionCommand(Guid RegionId, IReadOnlyList<string>? Topics) : IRequest<Result<IReadOnlyList<SubscriptionResponse>>>;

internal sealed class SubscribeToRegionCommandValidator : AbstractValidator<SubscribeToRegionCommand>
{
    public SubscribeToRegionCommandValidator()
    {
        RuleFor(x => x.RegionId).NotEmpty();
        RuleForEach(x => x.Topics)
            .Must(t => Enum.TryParse<AlertTopics>(t, ignoreCase: true, out var v) && v is not AlertTopics.None && Enum.IsDefined(v))
            .WithMessage("Geçersiz konu. Geçerli değerler: Weather, Market, Community.");
    }
}

internal sealed class SubscribeToRegionCommandHandler(
    ICurrentUser currentUser,
    ISubscriptionRepository subscriptions,
    IRegionRepository regions,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<SubscribeToRegionCommand, Result<IReadOnlyList<SubscriptionResponse>>>
{
    public const int MaxSubscriptions = 10;

    public async Task<Result<IReadOnlyList<SubscriptionResponse>>> Handle(SubscribeToRegionCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<IReadOnlyList<SubscriptionResponse>>(IdentityErrors.Unauthenticated);
        }

        var region = await regions.GetByIdAsync(request.RegionId, cancellationToken);
        if (region is null || !region.IsActive)
        {
            return Result.Failure<IReadOnlyList<SubscriptionResponse>>(AlertErrors.RegionNotFound);
        }

        var topics = request.Topics is null or { Count: 0 }
            ? AlertTopics.Weather
            : request.Topics.Aggregate(AlertTopics.None, (acc, t) => acc | Enum.Parse<AlertTopics>(t, ignoreCase: true));

        var existing = await subscriptions.GetAsync(userId, region.Id, cancellationToken);
        if (existing is not null)
        {
            existing.ChangeTopics(topics);
        }
        else
        {
            var count = (await subscriptions.GetByUserAsync(userId, cancellationToken)).Count;
            if (count >= MaxSubscriptions)
            {
                return Result.Failure<IReadOnlyList<SubscriptionResponse>>(AlertErrors.SubscriptionLimit);
            }

            subscriptions.Add(RegionSubscription.Create(userId, region.Id, topics, clock.UtcNow));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await SubscriptionViews.BuildAsync(userId, subscriptions, regions, cancellationToken));
    }
}

public sealed record UnsubscribeFromRegionCommand(Guid RegionId) : IRequest<Result>;

internal sealed class UnsubscribeFromRegionCommandHandler(ICurrentUser currentUser, ISubscriptionRepository subscriptions, IUnitOfWork unitOfWork)
    : IRequestHandler<UnsubscribeFromRegionCommand, Result>
{
    public async Task<Result> Handle(UnsubscribeFromRegionCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(IdentityErrors.Unauthenticated);
        }

        var existing = await subscriptions.GetAsync(userId, request.RegionId, cancellationToken);
        if (existing is null)
        {
            return Result.Failure(AlertErrors.SubscriptionNotFound);
        }

        subscriptions.Remove(existing);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal static class SubscriptionViews
{
    public static async Task<IReadOnlyList<SubscriptionResponse>> BuildAsync(
        Guid userId, ISubscriptionRepository subscriptions, IRegionRepository regions, CancellationToken cancellationToken)
    {
        var mine = await subscriptions.GetByUserAsync(userId, cancellationToken);
        var allRegions = (await regions.GetActiveAsync(cancellationToken)).ToDictionary(r => r.Id);

        return mine
            .Where(s => allRegions.ContainsKey(s.RegionId))
            .Select(s =>
            {
                var r = allRegions[s.RegionId];
                var topics = Enum.GetValues<AlertTopics>()
                    .Where(t => t is not AlertTopics.None and not AlertTopics.All && s.Wants(t))
                    .Select(t => t.ToString())
                    .ToList();
                return new SubscriptionResponse(r.Id, r.Code, r.Name, topics, s.CreatedAt);
            })
            .OrderBy(s => s.RegionName, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: true))
            .ToList();
    }
}

// ---- Cihaz (push token) kaydı ----

public sealed record RegisterDeviceCommand(string Token, string Platform) : IRequest<Result>;

internal sealed class RegisterDeviceCommandValidator : AbstractValidator<RegisterDeviceCommand>
{
    public RegisterDeviceCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(DeviceRegistration.TokenMaxLength);
        RuleFor(x => x.Platform).Must(p => Enum.TryParse<DevicePlatform>(p, ignoreCase: true, out _)).WithMessage("Platform Android veya Ios olmalı.");
    }
}

internal sealed class RegisterDeviceCommandHandler(
    ICurrentUser currentUser,
    IDeviceRepository devices,
    IPersonalDataProtector protector,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<RegisterDeviceCommand, Result>
{
    public async Task<Result> Handle(RegisterDeviceCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(IdentityErrors.Unauthenticated);
        }

        var platform = Enum.Parse<DevicePlatform>(request.Platform, ignoreCase: true);
        var hash = protector.ComputeLookupHash("device:" + request.Token);
        var existing = await devices.GetByTokenHashAsync(hash, cancellationToken);

        if (existing is null)
        {
            devices.Add(DeviceRegistration.Register(userId, request.Token, hash, platform, clock.UtcNow));
        }
        else
        {
            existing.Touch(userId, platform, clock.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record UnregisterDeviceCommand(string Token) : IRequest<Result>;

internal sealed class UnregisterDeviceCommandValidator : AbstractValidator<UnregisterDeviceCommand>
{
    public UnregisterDeviceCommandValidator() => RuleFor(x => x.Token).NotEmpty().MaximumLength(DeviceRegistration.TokenMaxLength);
}

internal sealed class UnregisterDeviceCommandHandler(
    ICurrentUser currentUser,
    IDeviceRepository devices,
    IPersonalDataProtector protector,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UnregisterDeviceCommand, Result>
{
    public async Task<Result> Handle(UnregisterDeviceCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(IdentityErrors.Unauthenticated);
        }

        var existing = await devices.GetByTokenHashAsync(protector.ComputeLookupHash("device:" + request.Token), cancellationToken);
        if (existing is not null && existing.UserId == userId)
        {
            devices.Remove(existing);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
