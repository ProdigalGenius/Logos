using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SahaNet.Application.Alerts;
using SahaNet.Domain.Alerts;
using SahaNet.Domain.Notifications;
using SahaNet.Infrastructure.Options;

namespace SahaNet.Infrastructure.Push;

/// <summary>Geliştirme amaçlı: push'u yalnızca loglar.</summary>
internal sealed partial class LoggingPushSender(ILogger<LoggingPushSender> logger) : IPushSender
{
    public Task<PushResult> SendAsync(DeviceRegistration device, PushMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(message);
        LogPush(logger, device.Platform, message.Severity, message.Title);
        return Task.FromResult(PushResult.Sent);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Push ({Platform}, {Severity}): {Title}")]
    private static partial void LogPush(ILogger logger, DevicePlatform platform, AlertSeverity severity, string title);
}

/// <summary>Firebase Cloud Messaging. Kritik uyarılar yüksek öncelik ve kritik kanal ile gönderilir.</summary>
internal sealed partial class FcmPushSender : IPushSender
{
    private readonly FirebaseMessaging _messaging;
    private readonly PushOptions _options;
    private readonly ILogger<FcmPushSender> _logger;

    public FcmPushSender(IOptions<PushOptions> options, ILogger<FcmPushSender> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.FcmCredentialsPath))
        {
            throw new InvalidOperationException("Push:FcmCredentialsPath tanımlı değil.");
        }

        var credential = CredentialFactory.FromFile<ServiceAccountCredential>(_options.FcmCredentialsPath).ToGoogleCredential();
        var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions { Credential = credential });
        _messaging = FirebaseMessaging.GetMessaging(app);
    }

    public async Task<PushResult> SendAsync(DeviceRegistration device, PushMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(message);

        var critical = message.Severity == AlertSeverity.Critical;
        var fcmMessage = new Message
        {
            Token = device.Token,
            Notification = new Notification { Title = message.Title, Body = message.Body },
            Data = message.Data,
            Android = new AndroidConfig
            {
                Priority = critical ? Priority.High : Priority.Normal,
                Notification = new AndroidNotification
                {
                    ChannelId = critical ? _options.AndroidCriticalChannelId : _options.AndroidDefaultChannelId,
                    Sound = "default",
                },
            },
            Apns = new ApnsConfig
            {
                Headers = new Dictionary<string, string> { ["apns-priority"] = critical ? "10" : "5" },
                Aps = new Aps
                {
                    Sound = "default",
                    CustomData = new Dictionary<string, object> { ["interruption-level"] = critical ? "time-sensitive" : "active" },
                },
            },
        };

        try
        {
            await _messaging.SendAsync(fcmMessage, cancellationToken);
            return PushResult.Sent;
        }
        catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
        {
            return PushResult.InvalidToken;
        }
        catch (FirebaseMessagingException ex)
        {
            LogFailed(_logger, ex, device.Platform);
            return PushResult.Failed;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "FCM gönderimi başarısız ({Platform})")]
    private static partial void LogFailed(ILogger logger, Exception exception, DevicePlatform platform);
}
