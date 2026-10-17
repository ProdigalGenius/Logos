namespace SahaNet.Mobile.Core.Api;

public enum ApiOutcome
{
    Success = 0,

    /// <summary>Sunucu isteği reddetti (4xx); tekrar göndermek sonucu değiştirmez.</summary>
    Rejected = 1,

    /// <summary>Oturum geçersiz; kullanıcı tekrar giriş yapmalı.</summary>
    Unauthorized = 2,

    /// <summary>Ağ yok, zaman aşımı veya 5xx; daha sonra tekrar denenebilir.</summary>
    Transient = 3,
}

public sealed record ApiResult<T>(ApiOutcome Outcome, T? Value, ProblemDto? Problem)
{
    public bool IsSuccess => Outcome == ApiOutcome.Success;

    /// <summary>Kullanıcıya gösterilecek Türkçe hata mesajı.</summary>
    public string? ErrorMessage => Outcome switch
    {
        ApiOutcome.Success => null,
        ApiOutcome.Transient => "İnternet bağlantısı yok. Bağlantı gelince tekrar denenecek.",
        ApiOutcome.Unauthorized => "Oturumunuz sona erdi. Lütfen tekrar giriş yapın.",
        _ => Problem?.Detail ?? "İşlem yapılamadı.",
    };

    public static ApiResult<T> Ok(T value) => new(ApiOutcome.Success, value, null);

    public static ApiResult<T> Fail(ApiOutcome outcome, ProblemDto? problem = null) => new(outcome, default, problem);
}
