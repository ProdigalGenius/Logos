using SahaNet.Domain.Consents;
using SahaNet.Domain.Identity;

namespace SahaNet.Application.Identity.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<User?> GetByPhoneHashAsync(string phoneHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<User>> GetPendingDeletionRequestedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken);

    /// <summary>Yönetici araması: telefon özetiyle tam eşleşme veya ad içinde geçen metin.</summary>
    Task<(IReadOnlyList<User> Items, int Total)> SearchAsync(string? phoneHash, string? text, UserStatus? status, int page, int pageSize, CancellationToken cancellationToken);

    void Add(User user);

    void Remove(User user);
}

public interface IOtpChallengeRepository
{
    Task<OtpChallenge?> GetLatestAsync(string phoneHash, CancellationToken cancellationToken);

    Task<int> CountCreatedSinceAsync(string phoneHash, DateTimeOffset since, CancellationToken cancellationToken);

    void Add(OtpChallenge challenge);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> GetActiveByFamilyAsync(Guid familyId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken);

    void Add(RefreshToken token);
}

public interface IConsentRepository
{
    Task<IReadOnlyList<ConsentDocument>> GetActiveDocumentsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<UserConsent>> GetUserConsentsAsync(Guid userId, CancellationToken cancellationToken);

    void Add(UserConsent consent);
}
