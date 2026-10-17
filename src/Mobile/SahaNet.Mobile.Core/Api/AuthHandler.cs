using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SahaNet.Mobile.Core.Api;

/// <summary>
/// Erişim belirtecini ekler; 401 alınırsa yenileme belirteciyle bir kez yeniler ve isteği tekrarlar.
/// Aynı anda gelen 401'ler tek bir yenileme isteğine indirgenir (sunucu belirteci döndürdüğü için şart).
/// </summary>
public sealed class AuthHandler(ITokenStore tokens, Func<HttpClient> refreshClientFactory) : DelegatingHandler
{
    // HttpClientFactory işleyicileri kısa ömürlüdür; kilit tüm örnekler arasında paylaşılır.
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Content is not null)
        {
            await request.Content.LoadIntoBufferAsync(cancellationToken);
        }

        var current = await tokens.GetAsync();
        Attach(request, current);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized || current is null)
        {
            return response;
        }

        var refreshed = await RefreshAsync(current, cancellationToken);
        if (refreshed is null)
        {
            return response;
        }

        response.Dispose();
        using var retry = await CloneAsync(request, cancellationToken);
        Attach(retry, refreshed);
        return await base.SendAsync(retry, cancellationToken);
    }

    private static void Attach(HttpRequestMessage request, StoredTokens? current)
    {
        if (current is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", current.AccessToken);
        }
    }

    private async Task<StoredTokens?> RefreshAsync(StoredTokens failed, CancellationToken cancellationToken)
    {
        await RefreshLock.WaitAsync(cancellationToken);
        try
        {
            // Başka bir istek bu arada yenilediyse onu kullan.
            var latest = await tokens.GetAsync();
            if (latest is not null && latest.AccessToken != failed.AccessToken)
            {
                return latest;
            }

            using var client = refreshClientFactory();
            using var response = await client.PostAsJsonAsync("api/auth/token/refresh", new { refreshToken = failed.RefreshToken }, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // Yenileme reddedildi: oturum bitti, uygulama giriş ekranına döner.
                await tokens.ClearAsync();
                return null;
            }

            var auth = await response.Content.ReadFromJsonAsync<AuthResult>(cancellationToken);
            if (auth is null)
            {
                return null;
            }

            var stored = new StoredTokens(auth.AccessToken, auth.AccessTokenExpiresAt, auth.RefreshToken);
            await tokens.SetAsync(stored);
            return stored;
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage original, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri) { Version = original.Version };
        if (original.Content is not null)
        {
            var bytes = await original.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in original.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in original.Headers.Where(h => h.Key != "Authorization"))
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
