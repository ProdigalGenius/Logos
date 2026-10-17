using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Maui.Media;
using SahaNet.Mobile.Core.Api;
using SahaNet.Mobile.Core.Offline;

namespace SahaNet.Mobile.Services;

/// <summary>Belirteçler cihazın güvenli deposunda (Android Keystore / iOS Keychain).</summary>
public sealed class SecureTokenStore : ITokenStore
{
    private const string Key = "sahanet.tokens";

    public async Task<StoredTokens?> GetAsync()
    {
        var json = await SecureStorage.Default.GetAsync(Key);
        return json is null ? null : JsonSerializer.Deserialize<StoredTokens>(json, SahaNetApiClient.Json);
    }

    public Task SetAsync(StoredTokens tokens) =>
        SecureStorage.Default.SetAsync(Key, JsonSerializer.Serialize(tokens, SahaNetApiClient.Json));

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(Key);
        return Task.CompletedTask;
    }
}

/// <summary>Bağlantı geri geldiğinde senkronizasyon tetiklenir.</summary>
public sealed class MauiConnectivityMonitor : IConnectivityMonitor, IDisposable
{
    public MauiConnectivityMonitor() => Connectivity.Current.ConnectivityChanged += OnChanged;

    public bool IsOnline => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    public event EventHandler<bool>? ConnectivityChanged;

    public void Dispose() => Connectivity.Current.ConnectivityChanged -= OnChanged;

    private void OnChanged(object? sender, ConnectivityChangedEventArgs e) =>
        ConnectivityChanged?.Invoke(this, e.NetworkAccess == NetworkAccess.Internet);
}

/// <summary>Her form alanında sesli giriş: Türkçe konuşmayı metne çevirir.</summary>
public interface IVoiceInput
{
    Task<string?> ListenAsync(CancellationToken cancellationToken);
}

public sealed class VoiceInput : IVoiceInput
{
    private static readonly CultureInfo Turkish = new("tr-TR");

    public async Task<string?> ListenAsync(CancellationToken cancellationToken)
    {
        var speech = SpeechToText.Default;
        if (!await speech.RequestPermissions(cancellationToken))
        {
            return null;
        }

        var completion = new TaskCompletionSource<string?>();
        void OnCompleted(object? sender, SpeechToTextRecognitionResultCompletedEventArgs e) =>
            completion.TrySetResult(e.RecognitionResult.IsSuccessful ? e.RecognitionResult.Text : null);

        speech.RecognitionResultCompleted += OnCompleted;
        try
        {
            await speech.StartListenAsync(new SpeechToTextOptions { Culture = Turkish, ShouldReportPartialResults = false }, cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            await using var registration = timeout.Token.Register(() => completion.TrySetResult(null));
            return await completion.Task;
        }
        finally
        {
            speech.RecognitionResultCompleted -= OnCompleted;
            await speech.StopListenAsync(CancellationToken.None);
        }
    }
}
