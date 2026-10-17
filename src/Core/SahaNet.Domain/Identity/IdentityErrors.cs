using SahaNet.Domain.Common;

namespace SahaNet.Domain.Identity;

public static class IdentityErrors
{
    public static readonly Error PhoneInvalid = new("identity.phone_invalid", "Geçerli bir cep telefonu numarası girin (5XX XXX XX XX).", ErrorType.Validation);
    public static readonly Error OtpCooldown = new("identity.otp_cooldown", "Yeni kod istemeden önce biraz bekleyin.", ErrorType.TooManyRequests);
    public static readonly Error OtpHourlyLimit = new("identity.otp_hourly_limit", "Çok fazla kod istendi. Daha sonra tekrar deneyin.", ErrorType.TooManyRequests);
    public static readonly Error OtpInvalid = new("identity.otp_invalid", "Kod hatalı.", ErrorType.Unauthorized);
    public static readonly Error OtpExpired = new("identity.otp_expired", "Kodun süresi doldu. Yeni kod isteyin.", ErrorType.Unauthorized);
    public static readonly Error OtpLocked = new("identity.otp_locked", "Çok fazla hatalı deneme. Yeni kod isteyin.", ErrorType.TooManyRequests);
    public static readonly Error RefreshTokenInvalid = new("identity.refresh_invalid", "Oturum geçersiz. Tekrar giriş yapın.", ErrorType.Unauthorized);
    public static readonly Error Unauthenticated = new("identity.unauthenticated", "Giriş yapmanız gerekiyor.", ErrorType.Unauthorized);
    public static readonly Error UserNotFound = new("identity.user_not_found", "Kullanıcı bulunamadı.", ErrorType.NotFound);
    public static readonly Error NicheUnknown = new("identity.niche_unknown", "Seçilen meslek grubu bulunamadı.", ErrorType.Validation);
    public static readonly Error RegionUnknown = new("identity.region_unknown", "Seçilen il bulunamadı.", ErrorType.Validation);
    public static readonly Error DisplayNameInvalid = new("identity.display_name_invalid", "Ad en fazla 50 karakter olabilir.", ErrorType.Validation);
    public static readonly Error AccountPendingDeletion = new("identity.pending_deletion", "Hesap silinmek üzere işaretli.", ErrorType.Conflict);
    public static readonly Error AccountSuspended = new("identity.suspended", "Hesabınız askıya alındı. Destek ile iletişime geçin.", ErrorType.Forbidden);
    public static readonly Error AccountNotSuspended = new("identity.not_suspended", "Hesap askıda değil.", ErrorType.Conflict);
    public static readonly Error AccountNotPendingDeletion = new("identity.not_pending_deletion", "Hesap için silme talebi yok.", ErrorType.Conflict);
}
