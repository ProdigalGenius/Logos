using FluentValidation;
using MediatR;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Domain.Common;
using SahaNet.Domain.Identity;

namespace SahaNet.Application.Moderation;

public sealed record CreatedUserResponse(Guid UserId, string Phone, string Role, bool AlreadyExisted);

/// <summary>Yönetici, telefon numarasıyla kullanıcı ekler (kullanıcı ilk girişinde rıza ve bölge adımını tamamlar).</summary>
public sealed record CreateUserCommand(string Phone, string? Role) : IRequest<Result<CreatedUserResponse>>;

internal sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Role).Must(r => r is null || Enum.TryParse<UserRole>(r, true, out _));
    }
}

internal sealed class CreateUserCommandHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    IPersonalDataProtector protector,
    IModerationService audit,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<CreateUserCommand, Result<CreatedUserResponse>>
{
    public async Task<Result<CreatedUserResponse>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.RequireAdmin() is { } denied)
        {
            return Result.Failure<CreatedUserResponse>(denied);
        }

        var phoneResult = PhoneNumber.Create(request.Phone);
        if (phoneResult.IsFailure)
        {
            return Result.Failure<CreatedUserResponse>(phoneResult.Error);
        }

        var phone = phoneResult.Value;
        var phoneHash = protector.ComputeLookupHash(phone.Value);
        var user = await users.GetByPhoneHashAsync(phoneHash, cancellationToken);
        var existed = user is not null;
        if (user is null)
        {
            user = User.Register(phone, phoneHash, clock.UtcNow);
            users.Add(user);
        }

        if (request.Role is { } roleText)
        {
            user.AssignRole(Enum.Parse<UserRole>(roleText, true));
        }

        audit.Audit(currentUser.UserId!.Value, existed ? "user.update" : "user.create", "User", user.Id.ToString(), user.Role.ToString());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new CreatedUserResponse(user.Id, phone.Masked, user.Role.ToString(), existed));
    }
}
