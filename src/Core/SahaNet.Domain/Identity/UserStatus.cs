namespace SahaNet.Domain.Identity;

public enum UserStatus
{
    Active = 0,
    PendingDeletion = 1,
}

/// <summary>Yetki rolü. Yönetici paneli Faz 7'de; şimdilik itiraz kararı için kullanılır.</summary>
public enum UserRole
{
    User = 0,
    Moderator = 1,
    Admin = 2,
}
