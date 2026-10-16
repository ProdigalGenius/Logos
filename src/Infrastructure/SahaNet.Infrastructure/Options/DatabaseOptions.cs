using System.ComponentModel.DataAnnotations;

namespace SahaNet.Infrastructure.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    [Range(0, 10)]
    public int MaxRetryCount { get; init; } = 3;
}
