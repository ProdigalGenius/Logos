using FluentValidation;
using MediatR;
using SahaNet.Application.Abstractions;
using SahaNet.Application.Events;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Domain.Common;
using SahaNet.Domain.Consents;
using SahaNet.Domain.Identity;

namespace SahaNet.Application.Consents;

public sealed record ConsentDocumentResponse(Guid Id, string Type, string Version, string Title, string Content, bool IsRequired, DateTimeOffset PublishedAt);

public sealed record ConsentRecordResponse(Guid DocumentId, string Type, string Version, bool Granted, string Source, DateTimeOffset RecordedAt);

public sealed record MyConsentsResponse(
    IReadOnlyList<ConsentRecordResponse> History,
    IReadOnlyList<Guid> MissingRequiredDocumentIds,
    bool LocationSharingGranted);

internal static class ConsentMapping
{
    public static ConsentDocumentResponse ToResponse(this ConsentDocument d) =>
        new(d.Id, d.Type.ToString(), d.Version, d.Title, d.Content, d.IsRequired, d.PublishedAt);

    public static ConsentRecordResponse ToResponse(this UserConsent c) =>
        new(c.DocumentId, c.Type.ToString(), c.Version, c.Granted, c.Source, c.RecordedAt);
}

// ---- Yürürlükteki metinler (giriş öncesi de gösterilir) ----

public sealed record GetConsentDocumentsQuery : IRequest<IReadOnlyList<ConsentDocumentResponse>>;

internal sealed class GetConsentDocumentsQueryHandler(IConsentRepository consents)
    : IRequestHandler<GetConsentDocumentsQuery, IReadOnlyList<ConsentDocumentResponse>>
{
    public async Task<IReadOnlyList<ConsentDocumentResponse>> Handle(GetConsentDocumentsQuery request, CancellationToken cancellationToken)
    {
        var documents = await consents.GetActiveDocumentsAsync(cancellationToken);
        return documents.Select(d => d.ToResponse()).ToList();
    }
}

// ---- Rıza kararlarını kaydet ----

public sealed record ConsentDecision(Guid DocumentId, bool Granted);

public sealed record RecordConsentsCommand(IReadOnlyList<ConsentDecision> Decisions, string Source) : IRequest<Result<MyConsentsResponse>>;

internal sealed class RecordConsentsCommandValidator : AbstractValidator<RecordConsentsCommand>
{
    private static readonly string[] AllowedSources = ["mobile", "web", "api"];

    public RecordConsentsCommandValidator()
    {
        RuleFor(x => x.Decisions).NotEmpty().Must(d => d.Count <= 20);
        RuleForEach(x => x.Decisions).ChildRules(d => d.RuleFor(x => x.DocumentId).NotEmpty());
        RuleFor(x => x.Source).NotEmpty().Must(s => AllowedSources.Contains(s)).WithMessage("Kaynak mobile, web veya api olmalı.");
    }
}

internal sealed class RecordConsentsCommandHandler(
    ICurrentUser currentUser,
    IConsentRepository consents,
    IUnitOfWork unitOfWork,
    IPublisher publisher,
    IDateTimeProvider clock)
    : IRequestHandler<RecordConsentsCommand, Result<MyConsentsResponse>>
{
    public async Task<Result<MyConsentsResponse>> Handle(RecordConsentsCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<MyConsentsResponse>(IdentityErrors.Unauthenticated);
        }

        var documents = (await consents.GetActiveDocumentsAsync(cancellationToken)).ToDictionary(d => d.Id);
        var now = clock.UtcNow;
        var added = new List<UserConsent>();

        foreach (var decision in request.Decisions)
        {
            if (!documents.TryGetValue(decision.DocumentId, out var document))
            {
                return Result.Failure<MyConsentsResponse>(ConsentErrors.DocumentNotFound);
            }

            var record = UserConsent.Record(userId, document, decision.Granted, request.Source, now);
            consents.Add(record);
            added.Add(record);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (added.Any(c => c.Type == ConsentType.LocationSharing && !c.Granted))
        {
            // KVKK: rıza geri çekilince modüller mevcut konum paylaşımlarını hemen kaldırır.
            await publisher.Publish(DomainEventNotification.Create(new LocationSharingWithdrawn(userId)), cancellationToken);
        }

        var history = await consents.GetUserConsentsAsync(userId, cancellationToken);
        var all = history.Concat(added.Where(a => history.All(h => h.Id != a.Id))).ToList();
        return Result.Success(ConsentSummary.Build(documents.Values.ToList(), all));
    }
}

// ---- Rıza geçmişim ----

public sealed record GetMyConsentsQuery : IRequest<Result<MyConsentsResponse>>;

internal sealed class GetMyConsentsQueryHandler(ICurrentUser currentUser, IConsentRepository consents)
    : IRequestHandler<GetMyConsentsQuery, Result<MyConsentsResponse>>
{
    public async Task<Result<MyConsentsResponse>> Handle(GetMyConsentsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<MyConsentsResponse>(IdentityErrors.Unauthenticated);
        }

        var documents = await consents.GetActiveDocumentsAsync(cancellationToken);
        var history = await consents.GetUserConsentsAsync(userId, cancellationToken);
        return Result.Success(ConsentSummary.Build(documents, history));
    }
}

internal static class ConsentSummary
{
    public static MyConsentsResponse Build(IReadOnlyList<ConsentDocument> documents, IReadOnlyList<UserConsent> history) => new(
        history.OrderByDescending(c => c.RecordedAt).Select(c => c.ToResponse()).ToList(),
        ConsentPolicy.MissingRequired(documents, history).Select(d => d.Id).ToList(),
        ConsentPolicy.IsGranted(ConsentType.LocationSharing, documents, history));
}
