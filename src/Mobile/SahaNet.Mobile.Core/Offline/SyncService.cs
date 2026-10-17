using SahaNet.Mobile.Core.Api;

namespace SahaNet.Mobile.Core.Offline;

public interface IConnectivityMonitor
{
    bool IsOnline { get; }

    event EventHandler<bool>? ConnectivityChanged;
}

public static class OutboxKinds
{
    public const string RecordCatch = "catch.record";
    public const string SaveSpot = "spot.save";
}

/// <summary>Kuyruktaki bir nokta kaydı (yeni noktada Id = istemci kimliği).</summary>
public sealed record PendingSpot(Guid Id, bool IsNew, SaveSpotRequest Request);

public sealed record SyncResult(int Sent, int Rejected, int Remaining, bool Interrupted);

/// <summary>
/// Outbox'ı sırayla sunucuya gönderir. Ağ hatasında durur (sıra korunur, sonra tekrar dener);
/// sunucu kalıcı olarak reddederse (4xx) kaydı "ölü" işaretler ve kullanıcıya gösterilir.
/// İstemci kimlikleri sayesinde tekrar gönderim çift kayıt oluşturmaz.
/// </summary>
public sealed class SyncService : IDisposable
{
    private readonly LocalStore _store;
    private readonly ISahaNetApi _api;
    private readonly IConnectivityMonitor _connectivity;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SyncService(LocalStore store, ISahaNetApi api, IConnectivityMonitor connectivity)
    {
        _store = store;
        _api = api;
        _connectivity = connectivity;
        _connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    public event EventHandler<SyncResult>? Synced;

    public async Task<SyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        if (!_connectivity.IsOnline)
        {
            return new SyncResult(0, 0, (await _store.GetOutboxAsync(cancellationToken: cancellationToken)).Count, Interrupted: true);
        }

        // Aynı anda tek senkronizasyon; ikincisi beklemeden döner.
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            return new SyncResult(0, 0, -1, Interrupted: true);
        }

        try
        {
            int sent = 0, rejected = 0;
            var interrupted = false;
            foreach (var item in await _store.GetOutboxAsync(cancellationToken: cancellationToken))
            {
                var (outcome, error) = await SendAsync(item, cancellationToken);
                if (outcome == ApiOutcome.Success)
                {
                    await _store.RemoveAsync(item.Id, cancellationToken);
                    sent++;
                }
                else if (outcome == ApiOutcome.Rejected)
                {
                    await _store.MarkFailedAsync(item.Id, error, dead: true, cancellationToken);
                    rejected++;
                }
                else
                {
                    await _store.MarkFailedAsync(item.Id, error, dead: false, cancellationToken);
                    interrupted = true;
                    break;
                }
            }

            var result = new SyncResult(sent, rejected, (await _store.GetOutboxAsync(cancellationToken: cancellationToken)).Count, interrupted);
            Synced?.Invoke(this, result);
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _connectivity.ConnectivityChanged -= OnConnectivityChanged;
        _gate.Dispose();
    }

    private async Task<(ApiOutcome Outcome, string? Error)> SendAsync(OutboxItem item, CancellationToken cancellationToken)
    {
        switch (item.Kind)
        {
            case OutboxKinds.RecordCatch:
                var catchResult = await _api.RecordCatchAsync(LocalStore.Deserialize<RecordCatchRequest>(item), cancellationToken);
                return (catchResult.Outcome, catchResult.ErrorMessage);
            case OutboxKinds.SaveSpot:
                var pending = LocalStore.Deserialize<PendingSpot>(item);
                var spotResult = await _api.SaveSpotAsync(pending.IsNew ? null : pending.Id, pending.Request, cancellationToken);
                return (spotResult.Outcome, spotResult.ErrorMessage);
            default:
                return (ApiOutcome.Rejected, $"Bilinmeyen işlem: {item.Kind}");
        }
    }

    private async void OnConnectivityChanged(object? sender, bool online)
    {
        if (!online)
        {
            return;
        }

        try
        {
            await SyncAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or ObjectDisposedException)
        {
            // Bir sonraki tetiklemede tekrar denenir.
        }
    }
}
