using Microsoft.EntityFrameworkCore;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Domain.Consents;
using SahaNet.Domain.Identity;

namespace SahaNet.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(SahaNetDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByPhoneHashAsync(string phoneHash, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(u => u.PhoneHash == phoneHash, cancellationToken);

    public async Task<IReadOnlyList<User>> GetPendingDeletionRequestedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken) =>
        await db.Users
            .Where(u => u.Status == UserStatus.PendingDeletion && u.DeletionRequestedAt <= threshold)
            .ToListAsync(cancellationToken);

    public void Add(User user) => db.Users.Add(user);

    public void Remove(User user) => db.Users.Remove(user);
}

internal sealed class OtpChallengeRepository(SahaNetDbContext db) : IOtpChallengeRepository
{
    public Task<OtpChallenge?> GetLatestAsync(string phoneHash, CancellationToken cancellationToken) =>
        db.OtpChallenges
            .Where(o => o.PhoneHash == phoneHash)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int> CountCreatedSinceAsync(string phoneHash, DateTimeOffset since, CancellationToken cancellationToken) =>
        db.OtpChallenges.CountAsync(o => o.PhoneHash == phoneHash && o.CreatedAt >= since, cancellationToken);

    public void Add(OtpChallenge challenge) => db.OtpChallenges.Add(challenge);
}

internal sealed class RefreshTokenRepository(SahaNetDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByFamilyAsync(Guid familyId, CancellationToken cancellationToken) =>
        await db.RefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAt == null).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(cancellationToken);

    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);
}

internal sealed class ConsentRepository(SahaNetDbContext db) : IConsentRepository
{
    public async Task<IReadOnlyList<ConsentDocument>> GetActiveDocumentsAsync(CancellationToken cancellationToken) =>
        await db.ConsentDocuments.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.Type)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<UserConsent>> GetUserConsentsAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.UserConsents.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.RecordedAt)
            .ToListAsync(cancellationToken);

    public void Add(UserConsent consent) => db.UserConsents.Add(consent);
}
