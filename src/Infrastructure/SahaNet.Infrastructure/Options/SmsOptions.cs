namespace SahaNet.Infrastructure.Options;

public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    /// <summary>
    /// Yalnızca geliştirme ortamında true olmalı: SMS içeriği (OTP kodu) loga yazılır.
    /// </summary>
    public bool LogMessageContent { get; init; }

    public SmsProvider Provider { get; init; } = SmsProvider.Logging;

    public NetgsmOptions Netgsm { get; init; } = new();
}
