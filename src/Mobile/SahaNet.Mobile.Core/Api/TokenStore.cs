namespace SahaNet.Mobile.Core.Api;

public sealed record StoredTokens(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken);

/// <summary>Belirteç saklama. Uygulamada güvenli depolama (Keystore/Keychain) ile uygulanır.</summary>
public interface ITokenStore
{
    Task<StoredTokens?> GetAsync();

    Task SetAsync(StoredTokens tokens);

    Task ClearAsync();
}

public sealed class InMemoryTokenStore : ITokenStore
{
    private StoredTokens? _tokens;

    public Task<StoredTokens?> GetAsync() => Task.FromResult(_tokens);

    public Task SetAsync(StoredTokens tokens)
    {
        _tokens = tokens;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        _tokens = null;
        return Task.CompletedTask;
    }
}
