namespace SahaNet.Application.Abstractions;

/// <summary>Test edilebilir zaman kaynağı.</summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
