namespace SahaNet.Infrastructure.Options;

public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    /// <summary>
    /// Yalnızca geliştirme ortamında true olmalı: SMS içeriği (OTP kodu) loga yazılır.
    /// Gerçek sağlayıcı (Netgsm / İleti Merkezi) Faz 2'de eklenecek.
    /// </summary>
    public bool LogMessageContent { get; init; }
}
