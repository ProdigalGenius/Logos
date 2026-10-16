using MediatR;
using SahaNet.Application.Identity.Auth;

namespace SahaNet.Api.Endpoints;

internal static class AuthEndpoints
{
    public const string OtpRateLimitPolicy = "otp";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth").AllowAnonymous();

        group.MapPost("/otp/request", async (RequestOtpCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToHttp())
            .RequireRateLimiting(OtpRateLimitPolicy)
            .WithName("RequestOtp")
            .WithSummary("Telefona tek kullanımlık doğrulama kodu gönderir.");

        group.MapPost("/otp/verify", async (VerifyOtpCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToHttp())
            .RequireRateLimiting(OtpRateLimitPolicy)
            .WithName("VerifyOtp")
            .WithSummary("Kodu doğrular; gerekirse hesabı oluşturur ve oturum açar.");

        group.MapPost("/token/refresh", async (RefreshSessionCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToHttp())
            .WithName("RefreshToken")
            .WithSummary("Yenileme belirtecini döndürerek yeni belirteç çifti verir.");

        group.MapPost("/logout", async (LogoutCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToHttp())
            .WithName("Logout");

        return app;
    }
}
