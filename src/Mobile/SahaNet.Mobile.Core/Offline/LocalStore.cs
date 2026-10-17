using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SahaNet.Mobile.Core.Api;

namespace SahaNet.Mobile.Core.Offline;

public sealed record OutboxItem(Guid Id, string Kind, string Payload, DateTimeOffset CreatedAt, int Attempts, string? LastError, bool IsDead);

public sealed record CachedValue<T>(T Value, DateTimeOffset UpdatedAt);

/// <summary>
/// Cihazdaki SQLite deposu: okuma önbelleği (son bilinen veri) ve gönderilmeyi bekleyen işlemler (outbox).
/// Zayıf bağlantıda uygulama son veriyle çalışır; yazma işlemleri kuyruğa alınıp bağlantı gelince gönderilir.
/// </summary>
public sealed class LocalStore : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private LocalStore(SqliteConnection connection) => _connection = connection;

    public static async Task<LocalStore> OpenAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(databasePath == ":memory:" ? "Data Source=:memory:" : $"Data Source={databasePath}");
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS cache (key TEXT PRIMARY KEY, json TEXT NOT NULL, updated_at TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS outbox (
                id TEXT PRIMARY KEY, kind TEXT NOT NULL, payload TEXT NOT NULL, created_at TEXT NOT NULL,
                attempts INTEGER NOT NULL DEFAULT 0, last_error TEXT NULL, dead INTEGER NOT NULL DEFAULT 0);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        return new LocalStore(connection);
    }

    public async Task PutAsync<T>(string key, T value, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(
            "INSERT INTO cache(key, json, updated_at) VALUES ($k, $j, $u) ON CONFLICT(key) DO UPDATE SET json = $j, updated_at = $u",
            cancellationToken,
            ("$k", key),
            ("$j", JsonSerializer.Serialize(value, SahaNetApiClient.Json)),
            ("$u", now.ToString("O", CultureInfo.InvariantCulture)));
    }

    public async Task<CachedValue<T>?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = "SELECT json, updated_at FROM cache WHERE key = $k";
            command.Parameters.AddWithValue("$k", key);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            var value = JsonSerializer.Deserialize<T>(reader.GetString(0), SahaNetApiClient.Json);
            return value is null ? null : new CachedValue<T>(value, DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture));
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task EnqueueAsync<T>(Guid id, string kind, T payload, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            "INSERT OR REPLACE INTO outbox(id, kind, payload, created_at, attempts, last_error, dead) VALUES ($i, $k, $p, $c, 0, NULL, 0)",
            cancellationToken,
            ("$i", id.ToString()),
            ("$k", kind),
            ("$p", JsonSerializer.Serialize(payload, SahaNetApiClient.Json)),
            ("$c", now.ToString("O", CultureInfo.InvariantCulture)));

    /// <summary>Gönderilecek işlemler, oluşturulma sırasıyla (sıra önemli: önce nokta, sonra o noktadaki av).</summary>
    public async Task<IReadOnlyList<OutboxItem>> GetOutboxAsync(bool includeDead = false, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = includeDead
                ? "SELECT id, kind, payload, created_at, attempts, last_error, dead FROM outbox ORDER BY created_at, rowid"
                : "SELECT id, kind, payload, created_at, attempts, last_error, dead FROM outbox WHERE dead = 0 ORDER BY created_at, rowid";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var items = new List<OutboxItem>();
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new OutboxItem(
                    Guid.Parse(reader.GetString(0)),
                    reader.GetString(1),
                    reader.GetString(2),
                    DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
                    reader.GetInt32(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.GetInt32(6) == 1));
            }

            return items;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task RemoveAsync(Guid id, CancellationToken cancellationToken = default) =>
        ExecuteAsync("DELETE FROM outbox WHERE id = $i", cancellationToken, ("$i", id.ToString()));

    public Task MarkFailedAsync(Guid id, string? error, bool dead, CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            "UPDATE outbox SET attempts = attempts + 1, last_error = $e, dead = $d WHERE id = $i",
            cancellationToken,
            ("$i", id.ToString()),
            ("$e", (object?)error ?? DBNull.Value),
            ("$d", dead ? 1 : 0));

    /// <summary>Çıkış / hesap silme: cihazdaki tüm kişisel veri silinir (KVKK).</summary>
    public Task ClearAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync("DELETE FROM cache; DELETE FROM outbox;", cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        _lock.Dispose();
    }

    public static T Deserialize<T>(OutboxItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return JsonSerializer.Deserialize<T>(item.Payload, SahaNetApiClient.Json)!;
    }

    private async Task ExecuteAsync(string sql, CancellationToken cancellationToken, params (string Name, object Value)[] parameters)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = sql;
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }
}
