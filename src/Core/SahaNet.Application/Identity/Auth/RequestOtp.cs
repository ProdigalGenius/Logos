using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Application.Options;
using SahaNet.Domain.Common;
using SahaNet.Domain.Identity;

namespace SahaNet.Application.Identity.Auth;

public sealed record RequestOtpCommand(string Phone) : IRequest<Result<RequestOtpResponse>>;

public sealed record RequestOtpResponse(int ExpiresInSeconds, int ResendAfterSeconds);

internal sealed class RequestOtpCommandValidator : AbstractValidator<RequestOtpCommand>
{
    public RequestOtpCommandValidator() => RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
}

internal sealed class RequestOtpCommandHandler(
    IOtpChallengeRepository challenges,
    IPersonalDataProtector protector,
    IOtpCodeGenerator codeGenerator,
    ISmsSender smsSender,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    IOptions<IdentityOptions> options)
    : IRequestHandler<RequestOtpCommand, Result<RequestOtpResponse>>
{
    public async Task<Result<RequestOtpResponse>> Handle(RequestOtpCommand request, CancellationToken cancellationToken)
    {
        var phoneResult = PhoneNumber.Create(request.Phone);
        if (phoneResult.IsFailure)
        {
            return Result.Failure<RequestOtpResponse>(phoneResult.Error);
        }

        var settings = options.Value;
        var phone = phoneResult.Value;
        var now = clock.UtcNow;
        var phoneHash = protector.ComputeLookupHash(phone.Value);

        var latest = await challenges.GetLatestAsync(phoneHash, cancellationToken);
        if (latest is not null && now - latest.CreatedAt < settings.OtpResendCooldown)
        {
            return Result.Failure<RequestOtpResponse>(IdentityErrors.OtpCooldown);
        }

        var sentLastHour = await challenges.CountCreatedSinceAsync(phoneHash, now.AddHours(-1), cancellationToken);
        if (sentLastHour >= settings.OtpMaxPerHour)
        {
            return Result.Failure<RequestOtpResponse>(IdentityErrors.OtpHourlyLimit);
        }

        var code = codeGenerator.Generate(settings.OtpLength);
        var challenge = OtpChallenge.Create(phoneHash, OtpHashing.HashCode(protector, phoneHash, code), now, settings.OtpLifetime);
        challenges.Add(challenge);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await smsSender.SendAsync(phone, $"{settings.SmsBrandName} doğrulama kodunuz: {code}. Bu kodu kimseyle paylaşmayın.", cancellationToken);

        return Result.Success(new RequestOtpResponse(
            (int)settings.OtpLifetime.TotalSeconds,
            (int)settings.OtpResendCooldown.TotalSeconds));
    }
}

internal static class OtpHashing
{
    /// <summary>Kod, telefon özetiyle birlikte özetlenir; aynı kod farklı numaralarda farklı özet üretir.</summary>
    public static string HashCode(IPersonalDataProtector protector, string phoneHash, string code) =>
        protector.ComputeLookupHash($"otp:{phoneHash}:{code}");
}
