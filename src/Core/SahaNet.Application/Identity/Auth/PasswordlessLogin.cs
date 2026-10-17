using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Application.Options;
using SahaNet.Domain.Common;
using SahaNet.Domain.Identity;

namespace SahaNet.Application.Identity.Auth;

/// <summary>
/// Yalnızca geliştirme/deneme: SMS doğrulaması olmadan giriş. Uç nokta yalnızca Development ortamında ve
/// <c>Development:PasswordlessLogin</c> açıkken eşlenir; üretimde bu komutu çağıran bir yol yoktur.
/// </summary>
public sealed record PasswordlessLoginCommand(string Phone) : IRequest<Result<AuthResponse>>;

internal sealed class PasswordlessLoginCommandValidator : AbstractValidator<PasswordlessLoginCommand>
{
    public PasswordlessLoginCommandValidator() => RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
}

internal sealed class PasswordlessLoginCommandHandler(
    IUserRepository users,
    IPersonalDataProtector protector,
    TokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    IOptions<IdentityOptions> options)
    : IRequestHandler<PasswordlessLoginCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(PasswordlessLoginCommand request, CancellationToken cancellationToken)
    {
        var phoneResult = PhoneNumber.Create(request.Phone);
        if (phoneResult.IsFailure)
        {
            return Result.Failure<AuthResponse>(phoneResult.Error);
        }

        var phone = phoneResult.Value;
        var now = clock.UtcNow;
        var phoneHash = protector.ComputeLookupHash(phone.Value);

        var user = await users.GetByPhoneHashAsync(phoneHash, cancellationToken);
        if (user is { Status: UserStatus.Suspended })
        {
            return Result.Failure<AuthResponse>(IdentityErrors.AccountSuspended);
        }

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
