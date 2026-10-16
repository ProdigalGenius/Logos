using FluentValidation;
using MediatR;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Common;
using SahaNet.Application.Consents;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Application.Modules;
using SahaNet.Domain.Common;
using SahaNet.Domain.Consents;
using SahaNet.Domain.Identity;

namespace SahaNet.Application.Identity.Profile;

public sealed record ProfileResponse(
    Guid UserId,
    string Phone,
    string? DisplayName,
    string? NicheKey,
    string? RegionCode,
    string? RegionName,
    bool IsOnboarded,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeletionRequestedAt);

internal static class ProfileMapping
{
    public static ProfileResponse ToResponse(this User user) => new(
        user.Id,
        user.Phone.Value,
        user.DisplayName,
        user.NicheKey,
        user.RegionCode,
        RegionCatalog.Find(user.RegionCode)?.Name,
        user.IsOnboarded,
        user.Status.ToString(),
        user.CreatedAt,
        user.DeletionRequestedAt);
}

internal static class CurrentUserExtensions
{
    public static async Task<Result<User>> LoadAsync(this ICurrentUser currentUser, IUserRepository users, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<User>(IdentityErrors.Unauthenticated);
        }

        var user = await users.GetByIdAsync(userId, cancellationToken);
        return user is null ? Result.Failure<User>(IdentityErrors.UserNotFound) : Result.Success(user);
    }
}

// ---- Profilimi getir ----

public sealed record GetMyProfileQuery : IRequest<Result<ProfileResponse>>;

internal sealed class GetMyProfileQueryHandler(ICurrentUser currentUser, IUserRepository users)
    : IRequestHandler<GetMyProfileQuery, Result<ProfileResponse>>
{
    public async Task<Result<ProfileResponse>> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await currentUser.LoadAsync(users, cancellationToken);
        return user.IsFailure ? Result.Failure<ProfileResponse>(user.Error) : Result.Success(user.Value.ToResponse());
    }
}

// ---- Kurulum (onboarding): niş + bölge seçimi ----

public sealed record CompleteOnboardingCommand(string NicheKey, string RegionCode, string? DisplayName) : IRequest<Result<ProfileResponse>>;

internal sealed class CompleteOnboardingCommandValidator : AbstractValidator<CompleteOnboardingCommand>
{
    public CompleteOnboardingCommandValidator()
    {
        RuleFor(x => x.NicheKey).NotEmpty().MaximumLength(32);
        RuleFor(x => x.RegionCode).NotEmpty().Length(2);
        RuleFor(x => x.DisplayName).MaximumLength(User.DisplayNameMaxLength);
    }
}

internal sealed class CompleteOnboardingCommandHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    IConsentRepository consents,
    IEnumerable<IModuleDescriptor> modules,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CompleteOnboardingCommand, Result<ProfileResponse>>
{
    public async Task<Result<ProfileResponse>> Handle(CompleteOnboardingCommand request, CancellationToken cancellationToken)
    {
        var userResult = await currentUser.LoadAsync(users, cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<ProfileResponse>(userResult.Error);
        }

        if (!modules.Any(m => string.Equals(m.Key, request.NicheKey, StringComparison.Ordinal)))
        {
            return Result.Failure<ProfileResponse>(IdentityErrors.NicheUnknown);
        }

        if (RegionCatalog.Find(request.RegionCode) is null)
        {
            return Result.Failure<ProfileResponse>(IdentityErrors.RegionUnknown);
        }

        var user = userResult.Value;
        var documents = await consents.GetActiveDocumentsAsync(cancellationToken);
        var given = await consents.GetUserConsentsAsync(user.Id, cancellationToken);
        var missing = ConsentPolicy.MissingRequired(documents, given);
        if (missing.Count != 0)
        {
            return Result.Failure<ProfileResponse>(ConsentErrors.RequiredMissing(missing));
        }

        var result = user.CompleteOnboarding(request.NicheKey, request.RegionCode, request.DisplayName);
        if (result.IsFailure)
        {
            return Result.Failure<ProfileResponse>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(user.ToResponse());
    }
}

// ---- Profil güncelle ----

public sealed record UpdateProfileCommand(string? DisplayName, string? RegionCode) : IRequest<Result<ProfileResponse>>;

internal sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.DisplayName).MaximumLength(User.DisplayNameMaxLength);
        RuleFor(x => x.RegionCode).Length(2).When(x => x.RegionCode is not null);
    }
}

internal sealed class UpdateProfileCommandHandler(ICurrentUser currentUser, IUserRepository users, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateProfileCommand, Result<ProfileResponse>>
{
    public async Task<Result<ProfileResponse>> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var userResult = await currentUser.LoadAsync(users, cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<ProfileResponse>(userResult.Error);
        }

        if (request.RegionCode is not null && RegionCatalog.Find(request.RegionCode) is null)
        {
            return Result.Failure<ProfileResponse>(IdentityErrors.RegionUnknown);
        }

        var user = userResult.Value;
        var result = user.UpdateProfile(request.DisplayName, request.RegionCode);
        if (result.IsFailure)
        {
            return Result.Failure<ProfileResponse>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(user.ToResponse());
    }
}
