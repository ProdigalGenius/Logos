using System.Text.Json;
using SahaNet.Application.Abstractions;
using SahaNet.Domain.Moderation;

namespace SahaNet.Application.Moderation;

internal sealed class ModerationService(IModerationRepository repository, IUnitOfWork unitOfWork, IDateTimeProvider clock) : IModerationService
{
    public async Task FlagAsync(ReportTargetType type, Guid targetId, Guid ownerId, SpamResult spam, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spam);
        repository.Add(ContentReport.BySpamFilter(type, targetId, ownerId, spam.Score, string.Join("; ", spam.Reasons), clock.UtcNow));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public void Audit(Guid actorId, string action, string targetType, string targetId, string? details) =>
        repository.Add(AuditEntry.Record(actorId, action, targetType, targetId, details, clock.UtcNow));

    public static string Describe(object value) => JsonSerializer.Serialize(value);
}
