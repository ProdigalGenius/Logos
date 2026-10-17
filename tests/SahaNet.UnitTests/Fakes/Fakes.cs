using SahaNet.Application.Abstractions;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Application.Modules;
using SahaNet.Domain.Consents;
using SahaNet.Domain.Identity;

namespace SahaNet.UnitTests.Fakes;

internal sealed class FakeClock : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 16, 8, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan by) => UtcNow += by;
}

internal sealed class FakeProtector : IPersonalDataProtector
{
    public string Protect(string plaintext) => "enc:" + plaintext;

    public string Unprotect(string ciphertext) => ciphertext["enc:".Length..];

    public string ComputeLookupHash(string value) => "h:" + value;
}

internal sealed class FakeOtpGenerator : IOtpCodeGenerator
{
    public string NextCode { get; set; } = "123456";

    public string Generate(int length) => NextCode;
}

internal sealed class FakeSmsSender : ISmsSender
{
    public List<(PhoneNumber To, string Message)> Sent { get; } = [];

    public Task SendAsync(PhoneNumber to, string message, CancellationToken cancellationToken)
    {
        Sent.Add((to, message));
        return Task.CompletedTask;
    }
}

internal sealed class FakeTokenService(FakeClock clock) : ITokenService
{
    public AccessToken CreateAccessToken(User user) => new($"access-{user.Id}", clock.UtcNow.AddMinutes(15));

    public string GenerateRefreshToken() => $"refresh-{Guid.NewGuid():N}";
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}

internal sealed class FakePublisher : MediatR.IPublisher
{
    public List<object> Published { get; } = [];

    public Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        Published.Add(notification);
        return Task.CompletedTask;
    }

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : MediatR.INotification
    {
        Published.Add(notification);
        return Task.CompletedTask;
    }
}

internal sealed class FakeCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }

    public bool IsAdmin { get; set; }

    public bool IsModerator { get; set; }
}

internal sealed class FakeModule(string key) : IModuleDescriptor
{
    public string Key => key;

    public string DisplayName => key;
}

internal sealed class InMemoryUsers : IUserRepository
{
    public List<User> Items { get; } = [];

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(u => u.Id == id));

    public Task<User?> GetByPhoneHashAsync(string phoneHash, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(u => u.PhoneHash == phoneHash));

    public Task<IReadOnlyList<User>> GetPendingDeletionRequestedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<User>>(Items.Where(u => u.Status == UserStatus.PendingDeletion && u.DeletionRequestedAt <= threshold).ToList());

    public void Add(User user) => Items.Add(user);

    public void Remove(User user) => Items.Remove(user);
}

internal sealed class InMemoryOtpChallenges : IOtpChallengeRepository
{
    public List<OtpChallenge> Items { get; } = [];

    public Task<OtpChallenge?> GetLatestAsync(string phoneHash, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Where(o => o.PhoneHash == phoneHash).OrderByDescending(o => o.CreatedAt).FirstOrDefault());

    public Task<int> CountCreatedSinceAsync(string phoneHash, DateTimeOffset since, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Count(o => o.PhoneHash == phoneHash && o.CreatedAt >= since));

    public void Add(OtpChallenge challenge) => Items.Add(challenge);
}

internal sealed class InMemoryRefreshTokens : IRefreshTokenRepository
{
    public List<RefreshToken> Items { get; } = [];

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task<IReadOnlyList<RefreshToken>> GetActiveByFamilyAsync(Guid familyId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RefreshToken>>(Items.Where(t => t.FamilyId == familyId && !t.IsRevoked).ToList());

    public Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RefreshToken>>(Items.Where(t => t.UserId == userId && !t.IsRevoked).ToList());

    public void Add(RefreshToken token) => Items.Add(token);
}

internal sealed class InMemoryConsents : IConsentRepository
{
    public List<ConsentDocument> Documents { get; } = [];

    public List<UserConsent> Records { get; } = [];

    public Task<IReadOnlyList<ConsentDocument>> GetActiveDocumentsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ConsentDocument>>(Documents.Where(d => d.IsActive).ToList());

    public Task<IReadOnlyList<UserConsent>> GetUserConsentsAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<UserConsent>>(Records.Where(c => c.UserId == userId).ToList());

    public void Add(UserConsent consent) => Records.Add(consent);
}
