using Android.App;
using Android.Runtime;

namespace SahaNet.Mobile;

#if DEBUG
// Test APK'sı yerel ağdaki geliştirme sunucusuna (http) bağlanabilsin; yayın sürümü yalnızca HTTPS kullanır.
[Application(UsesCleartextTraffic = true)]
#else
[Application]
#endif
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    public override void OnCreate()
    {
        base.OnCreate();
        NotificationChannels.Create(this);
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
