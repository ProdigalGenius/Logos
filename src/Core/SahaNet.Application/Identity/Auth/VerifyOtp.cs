using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Application.Options;
using SahaNet.Domain.Common;
using SahaNet.Domain.Identity;

namespace SahaNet.Application.Identity.Auth;

public sealed record VerifyOtpCommand(string Phone, string Code) : IRequest<Result<AuthResponse>>;

internal sealed class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpCommandValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(8).Matches("^[0-9]+$");
    }
}

internal sealed class VerifyOtpCommandHandler(
    IOtpChallengeRepository challenges,
    IUserRepository users,
    IPersonalDataProtector protector,
    TokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    IOptions<IdentityOptions> options)
    : IRequestHandler<VerifyOtpCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var phoneResult = PhoneNumber.Create(request.Phone);
        if (phoneResult.IsFailure)
        {
            return Result.Failure<AuthResponse>(phoneResult.Error);
        }

        var phone = phoneResult.Value;
        var now = clock.UtcNow;
        var phoneHash = protector.ComputeLookupHash(phone.Value);

        var challenge = await challenges.GetLatestAsync(phoneHash, cancellationToken);
        if (challenge is null)
        {
            return Result.Failure<AuthResponse>(IdentityErrors.OtpInvalid);
        }

        var outcome = challenge.Verify(OtpHashing.HashCode(protector, phoneHash, request.Code), now, options.Value.OtpMaxAttempts);
        if (outcome != OtpVerificationOutcome.Success)
        {
            // Hatalı deneme sayacı kalıcı olmalı (brute force koruması).
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthResponse>(outcome switch
            {
                OtpVerificationOutcome.Expired => IdentityErrors.OtpExpired,
                OtpVerificationOutcome.Locked => IdentityErrors.OtpLocked,
                _ => IdentityErrors.OtpInvalid,
            });
        }

        var user = await users.GetByPhoneHashAsync(phoneHash, cancellationToken);
        var isNewUser = user is null;
        if (user is null)
        {
            user = User.Register(phone, phoneHash, now);
            users.Add(user);
        }

        user.RecordLogin(now);
        if (user.Role == UserRole.User && options.Value.AdminPhones.Any(p => PhoneNumber.Create(p) is { IsSuccess: true } a && a.Value == phone))
        {
            user.AssignRole(UserRole.Admin);
        }

        var (response, _) = tokenIssuer.Issue(user, Guid.NewGuid(), isNewUser);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(response);
    }
}
