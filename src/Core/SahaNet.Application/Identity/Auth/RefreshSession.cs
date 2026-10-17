using FluentValidation;
using MediatR;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Domain.Common;
using SahaNet.Domain.Identity;

namespace SahaNet.Application.Identity.Auth;

public sealed record RefreshSessionCommand(string RefreshToken) : IRequest<Result<AuthResponse>>;

internal sealed class RefreshSessionCommandValidator : AbstractValidator<RefreshSessionCommand>
{
    public RefreshSessionCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(256);
}

internal sealed class RefreshSessionCommandHandler(
    IRefreshTokenRepository refreshTokens,
    IUserRepository users,
    IPersonalDataProtector protector,
    TokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<RefreshSessionCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(RefreshSessionCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var stored = await refreshTokens.GetByHashAsync(protector.ComputeLookupHash(request.RefreshToken), cancellationToken);
        if (stored is null)
        {
            return Result.Failure<AuthResponse>(IdentityErrors.RefreshTokenInvalid);
        }

        if (stored.IsRevoked)
        {
            // Döndürülmüş bir belirteç tekrar kullanıldı: çalınmış olabilir, tüm aileyi iptal et.
            foreach (var token in await refreshTokens.GetActiveByFamilyAsync(stored.FamilyId, cancellationToken))
            {
                token.Revoke(now);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthResponse>(IdentityErrors.RefreshTokenInvalid);
        }

        if (!stored.IsActive(now))
        {
            return Result.Failure<AuthResponse>(IdentityErrors.RefreshTokenInvalid);
        }

        var user = await users.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null || user.Status == UserStatus.Suspended)
        {
            return Result.Failure<AuthResponse>(IdentityErrors.RefreshTokenInvalid);
        }

        var (response, replacement) = tokenIssuer.Issue(user, stored.FamilyId, isNewUser: false);
        stored.Revoke(now, replacement.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(response);
    }
}
