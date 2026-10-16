using SahaNet.Domain.Common;

namespace SahaNet.Modules.Fishing.Domain;

public static class FishingErrors
{
    public static readonly Error PortNotFound = new("fishing.port_not_found", "Liman bulunamadı.", ErrorType.NotFound);
    public static readonly Error SpotNotFound = new("fishing.spot_not_found", "Av noktası bulunamadı.", ErrorType.NotFound);
    public static readonly Error SpotLimitReached = new("fishing.spot_limit", "Ücretsiz sürümde en fazla 20 av noktası kaydedebilirsiniz.", ErrorType.Conflict);
    public static readonly Error SpeciesUnknown = new("fishing.species_unknown", "Tür bulunamadı.", ErrorType.Validation);
    public static readonly Error CatchNotFound = new("fishing.catch_not_found", "Av kaydı bulunamadı.", ErrorType.NotFound);
    public static readonly Error LocationConsentRequired = new("fishing.location_consent_required", "Konum paylaşmak için Konum Paylaşımı Rızası vermeniz gerekiyor.", ErrorType.Forbidden);
    public static readonly Error ShareTargetNotFound = new("fishing.share_target_not_found", "Bu numarayla kayıtlı kullanıcı yok.", ErrorType.NotFound);
    public static readonly Error CannotShareWithSelf = new("fishing.share_self", "Noktayı kendinizle paylaşamazsınız.", ErrorType.Validation);
    public static readonly Error CatchInFuture = new("fishing.catch_in_future", "Av tarihi gelecekte olamaz.", ErrorType.Validation);
}
