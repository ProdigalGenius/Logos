using SahaNet.Mobile.Views;

namespace SahaNet.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("sea", typeof(SeaStatusPage));
        Routing.RegisterRoute("catches", typeof(CatchLogPage));
    }
}
