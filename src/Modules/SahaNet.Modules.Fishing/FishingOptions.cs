using System.ComponentModel.DataAnnotations;

namespace SahaNet.Modules.Fishing;

public sealed class FishingOptions
{
    public const string SectionName = "Fishing";

    /// <summary>Ücretsiz sürümde kayıtlı av noktası sınırı (premium: sınırsız, Faz 5).</summary>
    [Range(1, 10_000)]
    public int FreeSpotLimit { get; init; } = 20;
}
