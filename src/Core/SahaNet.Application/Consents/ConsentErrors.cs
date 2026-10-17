using SahaNet.Domain.Common;
using SahaNet.Domain.Consents;

namespace SahaNet.Application.Consents;

public static class ConsentErrors
{
    public static readonly Error DocumentNotFound = new("consent.document_not_found", "Metin bulunamadı veya yürürlükte değil.", ErrorType.NotFound);

    public static Error RequiredMissing(IEnumerable<ConsentDocument> missing) => new(
        "consent.required_missing",
        "Devam etmek için şu metinleri onaylamanız gerekiyor: " + string.Join(", ", missing.Select(d => d.Title)),
        ErrorType.Validation);
}
