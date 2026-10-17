using SahaNet.Mobile.Core.Api;
using SahaNet.Mobile.Core.Offline;

namespace SahaNet.Mobile.Core.Data;

/// <summary>Önbellekten mi geldiği ve ne kadar eski olduğu bilgisiyle veri.</summary>
public sealed record Fresh<T>(T? Value, DateTimeOffset? AsOf, bool FromCache, string? Error)
{
    public bool HasValue => Value is not null;
}

/// <summary>Kuyruğa alınmış (henüz gönderilmemiş) kayıtlar listede "bekliyor" olarak görünür.</summary>
public sealed record CatchItem(CatchDto Catch, bool IsPending);

public sealed record SpotItem(SpotDto Spot, bool IsPending);

/// <summary>
/// Offline-first erişim: okumada önce ağ, olmazsa son bilinen veri; yazmada önce yerel kuyruk,
/// sonra arka planda senkronizasyon. Kullanıcı ağ durumunu düşünmek zorunda kalmaz.
/// </summary>
public sealed class FishingRepository(ISahaNetApi api, LocalStore store, SyncService sync, TimeProvider clock)
{
    public Task<Fresh<SeaStatusDto>> GetSeaStatusAsync(string portCode, CancellationToken cancellationToken = default) =>
        NetworkFirstAsync($"sea:{portCode}", ct => api.GetSeaStatusAsync(portCode, ct), cancellationToken);

    public Task<Fresh<IReadOnlyList<PortDto>>> GetPortsAsync(CancellationToken cancellationToken = default) =>
        NetworkFirstAsync("ports", ct => api.GetPortsAsync(null, null, ct), cancellationToken);

    public Task<Fresh<IReadOnlyList<SpeciesDto>>> GetSpeciesAsync(CancellationToken cancellationToken = default) =>
        NetworkFirstAsync("species", ct => api.GetSpeciesAsync(ct), cancellationToken);

    public Task<Fresh<IReadOnlyList<AlertDto>>> GetAlertsAsync(CancellationToken cancellationToken = default) =>
        NetworkFirstAsync("alerts", ct => api.GetActiveAlertsAsync(ct), cancellationToken);

    public async Task<Fresh<IReadOnlyList<CatchItem>>> GetCatchesAsync(CancellationToken cancellationToken = default)
    {
        var remote = await NetworkFirstAsync("catches", ct => api.GetCatchesAsync(ct), cancellationToken);
        var species = (await store.GetAsync<IReadOnlyList<SpeciesDto>>("species", cancellationToken))?.Value ?? [];
        var pending = (await store.GetOutboxAsync(cancellationToken: cancellationToken))
            .Where(i => i.Kind == OutboxKinds.RecordCatch)
            .Select(LocalStore.Deserialize<RecordCatchRequest>)
            .Select(r => new CatchItem(
                new CatchDto(r.ClientId ?? Guid.Empty, r.CaughtAt, r.SpeciesCode, species.FirstOrDefault(s => s.Code == r.SpeciesCode)?.Name ?? r.SpeciesCode,
                    r.WeightKg, r.Count, r.LengthCm, r.SpotId, r.PortId, r.Notes),
                IsPending: true))
            .ToList();

        var pendingIds = pending.Select(p => p.Catch.Id).ToHashSet();
        var merged = pending
            .Concat((remote.Value ?? []).Where(c => !pendingIds.Contains(c.Id)).Select(c => new CatchItem(c, false)))
            .OrderByDescending(c => c.Catch.CaughtAt)
            .ToList();
        return new Fresh<IReadOnlyList<CatchItem>>(merged, remote.AsOf, remote.FromCache, remote.Error);
    }

    public async Task<Fresh<IReadOnlyList<SpotItem>>> GetSpotsAsync(CancellationToken cancellationToken = default)
    {
        var remote = await NetworkFirstAsync("spots", ct => api.GetSpotsAsync(ct), cancellationToken);
        var now = clock.GetUtcNow();
        var pending = (await store.GetOutboxAsync(cancellationToken: cancellationToken))
            .Where(i => i.Kind == OutboxKinds.SaveSpot)
            .Select(LocalStore.Deserialize<PendingSpot>)
            .Select(p => new SpotItem(
                new SpotDto(p.Id, p.Request.Name, p.Request.Latitude, p.Request.Longitude, p.Request.DepthM, p.Request.Notes,
                    p.Request.Species ?? [], IsMine: true, ShareCount: 0, now),
                IsPending: true))
            .ToList();

        var pendingIds = pending.Select(p => p.Spot.Id).ToHashSet();
        var merged = pending
            .Concat((remote.Value ?? []).Where(s => !pendingIds.Contains(s.Id)).Select(s => new SpotItem(s, false)))
            .ToList();
        return new Fresh<IReadOnlyList<SpotItem>>(merged, remote.AsOf, remote.FromCache, remote.Error);
    }

    /// <summary>Av kaydını yerel kuyruğa yazar ve senkronizasyonu tetikler. Uyarılar çevrimiçiyse döner.</summary>
    public async Task<(Guid Id, IReadOnlyList<string> Warnings, bool Queued)> RecordCatchAsync(RecordCatchRequest draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var id = draft.ClientId ?? Guid.NewGuid();
        var request = draft with { ClientId = id };
        await store.EnqueueAsync(id, OutboxKinds.RecordCatch, request, clock.GetUtcNow(), cancellationToken);

        // Hemen göndermeyi dene; olmazsa kayıt kuyrukta kalır.
        var direct = await api.RecordCatchAsync(request, cancellationToken);
        if (direct.IsSuccess)
        {
            await store.RemoveAsync(id, cancellationToken);
            return (id, direct.Value!.Warnings, false);
        }

        if (direct.Outcome == ApiOutcome.Rejected)
        {
            await store.RemoveAsync(id, cancellationToken);
            throw new InvalidOperationException(direct.ErrorMessage);
        }

        return (id, [], true);
    }

    public async Task<(Guid Id, bool Queued)> SaveSpotAsync(Guid? existingId, SaveSpotRequest draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var id = existingId ?? draft.ClientId ?? Guid.NewGuid();
        var request = existingId is null ? draft with { ClientId = id } : draft;
        await store.EnqueueAsync(id, OutboxKinds.SaveSpot, new PendingSpot(id, existingId is null, request), clock.GetUtcNow(), cancellationToken);

        var result = await sync.SyncAsync(cancellationToken);
        return (id, result.Interrupted && result.Remaining != 0);
    }

    public async Task<int> PendingCountAsync(CancellationToken cancellationToken = default) =>
        (await store.GetOutboxAsync(cancellationToken: cancellationToken)).Count;

    public async Task<IReadOnlyList<OutboxItem>> FailedAsync(CancellationToken cancellationToken = default) =>
        (await store.GetOutboxAsync(includeDead: true, cancellationToken)).Where(i => i.IsDead).ToList();

    private async Task<Fresh<T>> NetworkFirstAsync<T>(string key, Func<CancellationToken, Task<ApiResult<T>>> fetch, CancellationToken cancellationToken)
    {
        var result = await fetch(cancellationToken);
        if (result.IsSuccess && result.Value is not null)
        {
            var now = clock.GetUtcNow();
            await store.PutAsync(key, result.Value, now, cancellationToken);
            return new Fresh<T>(result.Value, now, FromCache: false, null);
        }

        var cached = await store.GetAsync<T>(key, cancellationToken);
        return cached is null
            ? new Fresh<T>(default, null, FromCache: false, result.ErrorMessage)
            : new Fresh<T>(cached.Value, cached.UpdatedAt, FromCache: true, result.ErrorMessage);
    }
}
