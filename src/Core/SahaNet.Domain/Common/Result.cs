namespace SahaNet.Domain.Common;

/// <summary>Hatanın türü; API katmanında HTTP durum koduna eşlenir.</summary>
public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5,
    TooManyRequests = 6,
}

/// <summary>Bir hatanın makine tarafından okunabilir kodu, açıklaması ve türü.</summary>
public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}

/// <summary>İstisna fırlatmadan başarı/hata döndürmek için sonuç tipi.</summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (isSuccess == (error != Error.None))
        {
            throw new ArgumentException("Başarılı sonuç hata içeremez; başarısız sonuç hata içermelidir.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<T> Success<T>(T value) => new(value, true, Error.None);

    public static Result<T> Failure<T>(Error error) => new(default, false, error);
}

/// <summary>Değer taşıyan sonuç tipi.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, bool isSuccess, Error error)
        : base(isSuccess, error) => _value = value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Başarısız sonucun değeri okunamaz.");
}
