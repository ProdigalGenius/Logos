namespace SahaNet.Application.Identity.Abstractions;

public interface ICurrentUser
{
    /// <summary>Kimliği doğrulanmış kullanıcı yoksa null.</summary>
    Guid? UserId { get; }
}
