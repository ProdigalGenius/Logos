using Android.App;
using Android.Runtime;

namespace SahaNet.Mobile;

[Application]
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
