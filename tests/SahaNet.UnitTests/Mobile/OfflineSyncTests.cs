using SahaNet.Mobile.Core.Api;
using SahaNet.Mobile.Core.Data;
using SahaNet.Mobile.Core.Offline;
using SahaNet.Mobile.Core.ViewModels;

namespace SahaNet.UnitTests.Mobile;

public sealed class OfflineSyncTests : IAsyncLifetime
{
    private readonly FakeApi _api = new();
    private readonly FakeConnectivity _connectivity = new();
    private LocalStore _store = null!;
    private SyncService _sync = null!;
    private FishingRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _store = await LocalStore.OpenAsync(":memory:");
        _sync = new SyncService(_store, _api, _connectivity);
        _repository = new FishingRepository(_api, _store, _sync, TimeProvider.System);
    }

    public async Task DisposeAsync()
    {
        _sync.Dispose();
        await _store.DisposeAsync();
    }

    private static RecordCatchRequest Catch(string species = "levrek", double kg = 2.5) =>
        new(DateTimeOffset.UtcNow, species, kg, null, 30, null, null, null, null);

    [Fact]
    public async Task Online_CatchIsSentImmediately_WithWarnings()
    {
        var (_, warnings, queued) = await _repository.RecordCatchAsync(Catch() with { LengthCm = 20 });

        queued.Should().BeFalse();
        warnings.Should().ContainSingle();
        (await _repository.PendingCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Offline_CatchIsQueued_ShownAsPending_AndSentWhenOnline()
    {
        _api.Offline = true;
        _connectivity.IsOnline = false;

        var (id, _, queued) = await _repository.RecordCatchAsync(Catch());
        queued.Should().BeTrue();

        var list = await _repository.GetCatchesAsync();
        list.Value.Should().ContainSingle(c => c.Catch.Id == id && c.IsPending);

        _api.Offline = false;
        _connectivity.IsOnline = true; // bağlantı olayı senkronizasyonu tetikler
        var result = await _sync.SyncAsync();

        (await _repository.PendingCountAsync()).Should().Be(0);
        _api.RecordedCatches.Should().ContainSingle(c => c.ClientId == id, "istemci kimliği tekrar gönderimde çift kaydı engeller");
        (await _repository.GetCatchesAsync()).Value.Should().ContainSingle(c => c.Catch.Id == id && !c.IsPending);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Sync_StopsOnNetworkError_PreservingOrder()
    {
        _api.Offline = true;
        _connectivity.IsOnline = false;
        await _repository.SaveSpotAsync(null, new SaveSpotRequest("Taşlık", 41.1, 29.05, 12, null, ["levrek"], null));
        await _repository.RecordCatchAsync(Catch());
        _connectivity.IsOnline = true; // olay tetiklenir ama API hâlâ erişilemez

        var interrupted = await _sync.SyncAsync();
        interrupted.Interrupted.Should().BeTrue();
        interrupted.Remaining.Should().Be(2);

        _api.Offline = false;
        var done = await _sync.SyncAsync();

        done.Remaining.Should().Be(0);
        _api.SavedSpots.Should().ContainSingle(s => s.ClientId != null);
        _api.RecordedCatches.Should().ContainSingle();
    }

    [Fact]
    public async Task RejectedItem_IsMarkedDead_AndDoesNotBlockQueue()
    {
        _api.Offline = true;
        _connectivity.IsOnline = false;
        await _repository.RecordCatchAsync(Catch("balina"));
        await _repository.RecordCatchAsync(Catch("lufer"));
        _api.RejectSpecies.Add("balina");
        _api.Offline = false;
        _connectivity.IsOnline = true;

        await _sync.SyncAsync();

        (await _repository.PendingCountAsync()).Should().Be(0);
        (await _repository.FailedAsync()).Should().ContainSingle().Which.LastError.Should().Be("Tür bulunamadı.");
        _api.RecordedCatches.Should().ContainSingle(c => c.SpeciesCode == "lufer");
    }

    [Fact]
    public async Task SeaStatus_FallsBackToCache_WhenOffline()
    {
        _api.SeaStatus = new SeaStatusDto(
            new PortDto(Guid.NewGuid(), "sariyer", "Sarıyer", "34", 41.17, 29.06, null), ["istanbul-bogazi"], DateTimeOffset.UtcNow, "Caution", [], [], []);
        var vm = new SeaStatusViewModel(_repository, new SahaNet.Mobile.Core.Niche.NicheProfile("fishing", "BalıkçıNet", "#0D47A1", "34", "sariyer", [new("sea", "Deniz", "🌊", "sea")]));

        await vm.LoadCommand.ExecuteAsync(null);
        vm.StaleText.Should().BeNull();
        vm.VerdictColor.Should().Be("#F9A825");

        _api.Offline = true;
        await vm.LoadCommand.ExecuteAsync(null);

        vm.Status.Should().NotBeNull();
        vm.StaleText.Should().NotBeNull("çevrimdışıyken son bilinen veri gösterilir");
        vm.VerdictText.Should().Be(SahaNet.Mobile.Core.Localization.Strings.Get("Sea_Caution"));
    }

    [Fact]
    public async Task ClearAsync_RemovesAllLocalPersonalData()
    {
        _api.Offline = true;
        await _repository.RecordCatchAsync(Catch());
        await _store.PutAsync("spots", new[] { 1, 2 }, DateTimeOffset.UtcNow);

        await _store.ClearAsync();

        (await _repository.PendingCountAsync()).Should().Be(0);
        (await _store.GetAsync<int[]>("spots")).Should().BeNull();
    }
}
