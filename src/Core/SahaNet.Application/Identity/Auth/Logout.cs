using FluentValidation;
using MediatR;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Domain.Common;

namespace SahaNet.Application.Identity.Auth;

/// <summary>Cihazdaki oturumu (yenileme belirteci ailesini) kapatır. Belirteç bilinmiyorsa da başarılı döner.</summary>
public sealed record LogoutCommand(string RefreshToken) : IRequest<Result>;

internal sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(256);
}

internal sealed class LogoutCommandHandler(
    IRefreshTokenRepository refreshTokens,
    IPersonalDataProtector protector,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var stored = await refreshTokens.GetByHashAsync(protector.ComputeLookupHash(request.RefreshToken), cancellationToken);
        if (stored is null)
        {
            return Result.Success();
        }

        var now = clock.UtcNow;
        foreach (var token in await refreshTokens.GetActiveByFamilyAsync(stored.FamilyId, cancellationToken))
        {
            token.Revoke(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
