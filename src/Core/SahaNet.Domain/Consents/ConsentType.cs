namespace SahaNet.Domain.Consents;

/// <summary>KVKK kapsamındaki metin/rıza türleri.</summary>
public enum ConsentType
{
    /// <summary>Aydınlatma metni (okundu bilgisi).</summary>
    PrivacyNotice = 0,

    /// <summary>Kişisel verilerin işlenmesine açık rıza.</summary>
    ExplicitConsent = 1,

    /// <summary>Konum verisinin paylaşılmasına açık rıza (varsayılan: verilmemiş).</summary>
    LocationSharing = 2,

    /// <summary>Ticari elektronik ileti izni.</summary>
    Marketing = 3,
}
