using Kofrenot.Core.Tests.Server;

namespace Kofrenot.Core.Tests.Session;

public sealed class SyncServiceTests : IDisposable
{
    private readonly SessionFixture _fixture = new();
    private readonly ServerConnection _connection;
    private readonly SyncService _sync;

    public SyncServiceTests()
    {
        _connection = _fixture.Connection();
        _sync = new SyncService(_fixture.Settings, _fixture.Files, _connection, _fixture.Ui, _fixture.Log, _fixture.Timers);
    }

    private ManualTimer Every => _fixture.Timers.Created[0];

    private ManualTimer Soon => _fixture.Timers.Created[1];

    public void Dispose()
    {
        _sync.Dispose();
        _connection.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public void AVaultNotSyncedYet_SaysSo() =>
        Assert.Equal(new SyncStatus(null, null, 0), _sync.StatusOf(_fixture.AddServerVault()));

    [Fact]
    public async Task Paused_ItStopsSyncingEvery30Seconds_UntilResumed()
    {
        var store = _fixture.AddServerVault();

        _sync.Pause();
        Assert.Equal(Timeout.InfiniteTimeSpan, Every.DueTime);
        await _sync.ResumeAsync();

        Assert.Equal(SyncStatus.Every, Every.DueTime);
        Assert.Equal(SyncStatus.Every, Every.Period);
        Assert.Equal(ServerFailure.SignInNeeded, _sync.StatusOf(store).Failure);
    }

    [Fact]
    public async Task SignedOut_EachServerVault_NeedsASignIn()
    {
        var store = _fixture.AddServerVault();
        var ended = 0;
        _sync.Changed += () => ended++;

        await _sync.SyncNowAsync();

        Assert.Equal(ServerFailure.SignInNeeded, _sync.StatusOf(store).Failure);
        Assert.Equal(1, ended);
    }

    [Fact]
    public async Task SignedIn_EachServerVault_IsSynced()
    {
        var store = _fixture.AddServerVault();
        await SignInAsync();

        await _sync.SyncNowAsync();

        Assert.Equal(SyncLevel.Synced, _sync.StatusOf(store).Level);
        Assert.Contains(_fixture.Http.Requests, request => request.Path == $"/v1/stores/{store.Id}/feed?since=0");
        Assert.Contains(_fixture.Http.Requests, request => request.Path == $"/v1/stores/{store.Id}/members");
    }

    [Fact]
    public async Task AVaultOnAnotherServer_NeedsASignInThere()
    {
        var store = new StoreAddress(new Uri("https://other.example/"), Guid.NewGuid());
        _fixture.Settings.SetVaults(_fixture.Settings.Vaults.Add(new RegisteredVault(new VaultLocation(_fixture.Files.MirrorOf(store.Id), store))));
        await SignInAsync();

        await _sync.SyncNowAsync();

        Assert.Equal(ServerFailure.SignInNeeded, _sync.StatusOf(store).Failure);
    }

    [Fact]
    public async Task Offline_IsLoggedOnce_ThenTheSyncThatGoesThroughAgain()
    {
        var store = _fixture.AddServerVault();
        await SignInAsync();
        _fixture.Offline = true;

        await _sync.SyncNowAsync();
        await _sync.SyncNowAsync();
        _fixture.Offline = false;
        await _sync.SyncNowAsync();

        Assert.Single(_fixture.Log.Entries, entry => entry.Level == LogLevel.Info && entry.Message.Contains("not synced"));
        Assert.Contains(_fixture.Log.Entries, entry => entry.Message.Contains("synced again"));
        Assert.Equal(SyncLevel.Synced, _sync.StatusOf(store).Level);
    }

    [Fact]
    public async Task AMirrorThatCannotBeWritten_FailsItsSync()
    {
        var store = _fixture.AddServerVault();
        await SignInAsync();
        Directory.CreateDirectory(Path.GetDirectoryName(_fixture.Files.MirrorOf(store.Id))!);
        File.WriteAllText(_fixture.Files.MirrorOf(store.Id), "a file where the mirror goes");

        await _sync.SyncNowAsync();

        Assert.Equal(ServerFailure.Failed, _sync.StatusOf(store).Failure);
        Assert.Contains(_fixture.Log.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task OneStore_IsSyncedApart()
    {
        var store = _fixture.AddServerVault();
        await SignInAsync();

        var status = await _sync.SyncAsync(store);

        Assert.Equal(SyncLevel.Synced, status.Level);
        Assert.Equal(status, _sync.StatusOf(store));
    }

    [Fact]
    public async Task ASyncAskedWhileOneRuns_RunsOnceMoreAfterIt()
    {
        _fixture.AddServerVault();
        await SignInAsync();
        _fixture.FeedGate = new SemaphoreSlim(0);

        var first = _sync.SyncNowAsync();
        var second = _sync.SyncNowAsync();
        _fixture.FeedGate.Release(2);
        await first;

        Assert.Same(first, second);
        Assert.Equal(2, _fixture.Http.Requests.Count(request => request.Path.Contains("/feed")));
    }

    [Fact]
    public async Task TheTimer_SyncsEvery30Seconds_FromTheUiThread()
    {
        var store = _fixture.AddServerVault();

        Every.Fire();
        Assert.Equal(new SyncStatus(null, null, 0), _sync.StatusOf(store));
        _fixture.Ui.RunAll();
        await _sync.SyncNowAsync();

        Assert.Equal(SyncStatus.Every, Every.Period);
        Assert.Equal(ServerFailure.SignInNeeded, _sync.StatusOf(store).Failure);
    }

    [Fact]
    public void AChange_SyncsSoon()
    {
        Assert.Equal(Timeout.InfiniteTimeSpan, Soon.DueTime);

        _sync.Soon();
        Soon.Fire();

        Assert.Equal(TimeSpan.FromSeconds(2), Soon.DueTime);
        Assert.Equal(1, _fixture.Ui.Waiting);
    }

    [Fact]
    public async Task SigningIn_SyncsSoon()
    {
        await SignInAsync();

        Assert.Equal(TimeSpan.FromSeconds(2), Soon.DueTime);
    }

    [Fact]
    public void Disposing_StopsTheTimers()
    {
        _sync.Dispose();

        Assert.All(_fixture.Timers.Created, timer => Assert.True(timer.IsDisposed));
    }

    private async Task SignInAsync()
    {
        await _connection.SignInAsync(StubHandler.Server, "ada@example.com", "secret password");
        _fixture.Ui.RunAll();
    }
}
