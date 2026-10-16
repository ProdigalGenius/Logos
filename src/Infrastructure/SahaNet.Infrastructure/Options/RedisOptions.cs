using System.ComponentModel.DataAnnotations;

namespace SahaNet.Infrastructure.Options;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    [Required]
    public string ConnectionString { get; init; } = string.Empty;
}
