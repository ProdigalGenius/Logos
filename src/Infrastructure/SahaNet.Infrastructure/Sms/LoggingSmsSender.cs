using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Domain.Identity;
using SahaNet.Infrastructure.Options;

namespace SahaNet.Infrastructure.Sms;

/// <summary>Geliştirme amaçlı SMS gönderici: mesajı gönderir gibi loglar. Numara her zaman maskelenir.</summary>
internal sealed partial class LoggingSmsSender(ILogger<LoggingSmsSender> logger, IOptions<SmsOptions> options) : ISmsSender
{
    public Task SendAsync(PhoneNumber to, string message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(to);

        if (options.Value.LogMessageContent)
        {
            LogSmsWithContent(logger, to.Masked, message);
        }
        else
        {
            LogSms(logger, to.Masked);
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SMS gönderildi: {MaskedPhone}")]
    private static partial void LogSms(ILogger logger, string maskedPhone);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SMS gönderildi (GELİŞTİRME, içerik loglanıyor): {MaskedPhone} -> {Message}")]
    private static partial void LogSmsWithContent(ILogger logger, string maskedPhone, string message);
}
