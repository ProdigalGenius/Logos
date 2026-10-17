using Android.App;
using Android.Content;

namespace SahaNet.Mobile;

/// <summary>
/// Bildirim kanalları. Sunucu fırtına uyarılarını "critical_alerts" kanalıyla gönderir; bu kanal
/// Rahatsız Etmeyin modunu deler (kullanıcı ilk açılışta "Rahatsız Etmeyin erişimi" izni vermelidir).
/// Kanal kimlikleri sunucudaki Push:AndroidCriticalChannelId / AndroidDefaultChannelId ile aynıdır.
/// </summary>
internal static class NotificationChannels
{
    public const string Critical = "critical_alerts";
    public const string Default = "alerts";

    public static void Create(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26) || context.GetSystemService(Context.NotificationService) is not NotificationManager manager)
        {
            return;
        }

        var critical = new NotificationChannel(Critical, "Fırtına ve tehlike uyarıları", NotificationImportance.High)
        {
            Description = "Hayati uyarılar. Sessiz modda da çalar.",
            LockscreenVisibility = NotificationVisibility.Public,
        };
        critical.EnableVibration(true);
        critical.EnableLights(true);
        critical.SetBypassDnd(true);
        manager.CreateNotificationChannel(critical);

        var normal = new NotificationChannel(Default, "Bildirimler", NotificationImportance.Default)
        {
            Description = "Teklif, mesaj ve hava uyarıları.",
        };
        manager.CreateNotificationChannel(normal);
    }
}
