namespace SahaNet.Application.Identity.Auth;

public sealed record AuthResponse(
    Guid UserId,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    bool IsNewUser,
    bool RequiresOnboarding);
