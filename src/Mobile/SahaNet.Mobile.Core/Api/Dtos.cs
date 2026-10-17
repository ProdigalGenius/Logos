namespace SahaNet.Mobile.Core.Api;

// Sunucu sözleşmelerinin mobil kopyaları (mobil, sunucu derlemelerine bağımlı olmasın diye).

public sealed record RequestOtpResult(int ExpiresInSeconds, int ResendAfterSeconds);

public sealed record AuthResult(
    Guid UserId,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    bool IsNewUser,
    bool RequiresOnboarding);

public sealed record ProfileDto(Guid UserId, string Phone, string? DisplayName, string? NicheKey, string? RegionCode, string? RegionName, bool IsOnboarded);

public sealed record RegionDto(string Code, string Name);

public sealed record ConsentDocumentDto(Guid Id, string Type, string Version, string Title, string Content, bool IsRequired);

public sealed record ConsentDecisionDto(Guid DocumentId, bool Granted);

public sealed record PortDto(Guid Id, string Code, string Name, string ProvinceCode, double Latitude, double Longitude, double? DistanceKm);

public sealed record SeaHourDto(DateTimeOffset Time, double? WindKmh, double? GustKmh, int? Beaufort, string? WindFrom, string? WindName, double? WaveM, string Verdict);

public sealed record SeaDayDto(DateOnly Date, double? MaxWindKmh, double? MaxGustKmh, int? MaxBeaufort, string? DominantWindName, double? MaxWaveM, string Verdict, string Summary);

public sealed record AlertDto(Guid Id, Guid RegionId, string Kind, string Severity, string Title, string Message, DateTimeOffset ValidFrom, DateTimeOffset ValidTo);

public sealed record SeaStatusDto(
    PortDto Port,
    IReadOnlyList<string> RegionCodes,
    DateTimeOffset RetrievedAt,
    string Now,
    IReadOnlyList<SeaDayDto> Days,
    IReadOnlyList<SeaHourDto> Hours,
    IReadOnlyList<AlertDto> ActiveAlerts);

public sealed record SpeciesDto(string Code, string Name, string? ScientificName, int? MinLengthCm);

public sealed record SpotDto(
    Guid Id,
    string Name,
    double Latitude,
    double Longitude,
    double? DepthM,
    string? Notes,
    IReadOnlyList<string> Species,
    bool IsMine,
    int ShareCount,
    DateTimeOffset UpdatedAt);

public sealed record SaveSpotRequest(
    string Name,
    double Latitude,
    double Longitude,
    double? DepthM,
    string? Notes,
    IReadOnlyList<string>? Species,
    Guid? ClientId);

public sealed record CatchDto(
    Guid Id,
    DateTimeOffset CaughtAt,
    string SpeciesCode,
    string SpeciesName,
    double WeightKg,
    int? Count,
    double? LengthCm,
    Guid? SpotId,
    Guid? PortId,
    string? Notes);

public sealed record RecordCatchRequest(
    DateTimeOffset CaughtAt,
    string SpeciesCode,
    double WeightKg,
    int? Count,
    double? LengthCm,
    Guid? SpotId,
    Guid? PortId,
    string? Notes,
    Guid? ClientId);

public sealed record RecordCatchResult(CatchDto Catch, IReadOnlyList<string> Warnings);

public sealed record ProblemDto(string? Title, string? Detail, int? Status);
