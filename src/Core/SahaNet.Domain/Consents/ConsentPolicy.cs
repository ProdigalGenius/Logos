namespace SahaNet.Domain.Consents;

public static class ConsentPolicy
{
    /// <summary>
    /// Aktif ve zorunlu olup, kullanıcının en son kararı "verildi" olmayan metinleri döndürür.
    /// Yeni sürüm yayınlandığında eski sürüme verilen rıza yeterli sayılmaz.
    /// </summary>
    public static IReadOnlyList<ConsentDocument> MissingRequired(
        IEnumerable<ConsentDocument> activeDocuments,
        IEnumerable<UserConsent> userConsents)
    {
        ArgumentNullException.ThrowIfNull(activeDocuments);
        ArgumentNullException.ThrowIfNull(userConsents);

        var latestByDocument = userConsents
            .GroupBy(c => c.DocumentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.RecordedAt).First().Granted);

        return activeDocuments
            .Where(d => d.IsActive && d.IsRequired)
            .Where(d => !latestByDocument.TryGetValue(d.Id, out var granted) || !granted)
            .ToList();
    }

    /// <summary>Belirli bir tür için güncel rıza verilmiş mi (örn. konum paylaşımı).</summary>
    public static bool IsGranted(ConsentType type, IEnumerable<ConsentDocument> activeDocuments, IEnumerable<UserConsent> userConsents)
    {
        ArgumentNullException.ThrowIfNull(activeDocuments);
        ArgumentNullException.ThrowIfNull(userConsents);

        var document = activeDocuments.Where(d => d.IsActive && d.Type == type).OrderByDescending(d => d.PublishedAt).FirstOrDefault();
        if (document is null)
        {
            return false;
        }

        return userConsents
            .Where(c => c.DocumentId == document.Id)
            .OrderByDescending(c => c.RecordedAt)
            .Select(c => c.Granted)
            .FirstOrDefault();
    }
}
