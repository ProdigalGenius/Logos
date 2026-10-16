namespace SahaNet.Application.Abstractions;

/// <summary>Değişiklikleri tek işlemde kalıcı hale getirir ve domain event'lerini yayınlar.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
