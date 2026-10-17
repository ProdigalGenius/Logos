using SahaNet.Mobile.Views;

namespace SahaNet.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("sea", typeof(SeaStatusPage));
        Routing.RegisterRoute("catches", typeof(CatchLogPage));
        Routing.RegisterRoute("alerts", typeof(AlertsPage));
        Routing.RegisterRoute("spots", typeof(SpotsPage));
        Routing.RegisterRoute("market", typeof(MarketPage));
        Routing.RegisterRoute("messages", typeof(ConversationsPage));
        Routing.RegisterRoute("chat", typeof(ChatPage));
        Routing.RegisterRoute("prices", typeof(ScrapPricesPage));
        Routing.RegisterRoute("pickups", typeof(PickupsPage));
        Routing.RegisterRoute("settings", typeof(SettingsPage));
    }
}
