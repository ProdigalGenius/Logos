using SahaNet.Domain.Consents;

namespace SahaNet.Infrastructure.Persistence.Configurations;

/// <summary>
/// İlk sürüm metinler. UYARI: İçerikler taslaktır, yayından önce hukuk danışmanı onayı gerekir.
/// Yeni sürüm için mevcut kaydı değiştirmeyin; yeni Id ve Version ile ekleyip eskisini pasifleştirin.
/// </summary>
internal static class ConsentDocumentSeed
{
    public static readonly Guid PrivacyNoticeV1 = new("0b1a7c52-4a8e-4b0e-9a51-5e0f6a1c0001");
    public static readonly Guid ExplicitConsentV1 = new("0b1a7c52-4a8e-4b0e-9a51-5e0f6a1c0002");
    public static readonly Guid LocationSharingV1 = new("0b1a7c52-4a8e-4b0e-9a51-5e0f6a1c0003");
    public static readonly Guid MarketingV1 = new("0b1a7c52-4a8e-4b0e-9a51-5e0f6a1c0004");

    private static readonly DateTimeOffset Published = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    public static ConsentDocument[] All =>
    [
        new(PrivacyNoticeV1, ConsentType.PrivacyNotice, "1.0", "Aydınlatma Metni",
            "TASLAK — 6698 sayılı KVKK kapsamında veri sorumlusu, işlenen veriler (telefon, profil, konum), amaçlar, aktarım ve haklarınız hakkında bilgilendirme.",
            isRequired: true, Published),
        new(ExplicitConsentV1, ConsentType.ExplicitConsent, "1.0", "Açık Rıza Metni",
            "TASLAK — Hizmetin sunulması için kişisel verilerimin işlenmesine açık rıza veriyorum.",
            isRequired: true, Published),
        new(LocationSharingV1, ConsentType.LocationSharing, "1.0", "Konum Paylaşımı Rızası",
            "TASLAK — Konum verilerim varsayılan olarak gizlidir. Bu rızayla seçtiğim konumların paylaşılmasına izin veririm.",
            isRequired: false, Published),
        new(MarketingV1, ConsentType.Marketing, "1.0", "Ticari İleti İzni",
            "TASLAK — Kampanya ve duyurular için SMS/bildirim almayı kabul ediyorum.",
            isRequired: false, Published),
    ];
}
