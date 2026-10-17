using SahaNet.Domain.Common;

namespace SahaNet.Application.Alerts;

public static class AlertErrors
{
    public static readonly Error RegionNotFound = new("alerts.region_not_found", "Bölge bulunamadı.", ErrorType.NotFound);
    public static readonly Error SubscriptionNotFound = new("alerts.subscription_not_found", "Bu bölgeye aboneliğiniz yok.", ErrorType.NotFound);
    public static readonly Error SubscriptionLimit = new("alerts.subscription_limit", "En fazla 10 bölgeye abone olabilirsiniz.", ErrorType.Conflict);
}
