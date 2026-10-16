using MediatR;
using SahaNet.Application.Consents;
using SahaNet.Application.Identity.Privacy;
using SahaNet.Application.Identity.Profile;

namespace SahaNet.Api.Endpoints;

internal static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/me").WithTags("Me").RequireAuthorization();

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetMyProfileQuery(), ct)).ToHttp())
            .WithName("GetMyProfile");

        group.MapPost("/onboarding", async (CompleteOnboardingCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToHttp())
            .WithName("CompleteOnboarding")
            .WithSummary("Niş ve bölge seçimiyle kurulumu tamamlar (zorunlu rızalar verilmiş olmalı).");

        group.MapPut("/profile", async (UpdateProfileCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToHttp())
            .WithName("UpdateProfile");

        group.MapGet("/consents", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetMyConsentsQuery(), ct)).ToHttp())
            .WithName("GetMyConsents");

        group.MapPost("/consents", async (RecordConsentsCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToHttp())
            .WithName("RecordConsents");

        group.MapGet("/export", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ExportMyDataQuery(), ct)).ToHttp())
            .WithName("ExportMyData")
            .WithSummary("KVKK md. 11: kişisel verilerimin bir kopyası.");

        group.MapDelete("/", async (ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new RequestAccountDeletionCommand(), ct);
                return result.IsSuccess ? Results.Accepted(value: result.Value) : result.Error.ToProblem();
            })
            .WithName("RequestAccountDeletion")
            .WithSummary("Hesabı silinmek üzere işaretler; bekleme süresi sonunda kalıcı silinir.");

        group.MapPost("/deletion/cancel", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new CancelAccountDeletionCommand(), ct)).ToHttp())
            .WithName("CancelAccountDeletion");

        return app;
    }
}
