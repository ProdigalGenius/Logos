using SahaNet.Application.Alerts;
using SahaNet.Domain.Alerts;

namespace SahaNet.Application.Market;

/// <summary>Kullanıcının tüm cihazlarına (bilgi seviyesinde) push gönderir; geçersiz token'ları temizler.</summary>
public interface IUserNotifier
{
    Task NotifyAsync(Guid userId, string title, string body, IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken);
}

internal sealed class PushUserNotifier(IDeviceRepository devices, IPushSender push) : IUserNotifier
{
    public async Task NotifyAsync(Guid userId, string title, string body, IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken)
    {
        var message = new PushMessage(title, body, AlertSeverity.Info, data);
        foreach (var device in await devices.GetByUserAsync(userId, cancellationToken))
        {
            if (await push.SendAsync(device, message, cancellationToken) == PushResult.InvalidToken)
            {
                devices.Remove(device);
            }
        }
    }
}
