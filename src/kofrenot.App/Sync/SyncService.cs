using System.IO;
using System.Windows.Threading;
using Kofrenot.Core;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Server;
using Kofrenot.Core.Settings;

namespace Kofrenot.UI.Sync;

/// <summary>Where a server vault's sync stands.</summary>
/// <param name="Synced">When it last went through; null when not yet since kofrenot started.</param>
/// <param name="Failure">Why the last one failed; null when it went through.</param>
/// <param name="Waiting">Files of the mirror the store does not have yet.</param>
public sealed record SyncStatus(DateTimeOffset? Synced, ServerFailure? Failure, int Waiting)
{
    /// <summary>"synced", "offline, 3 files waiting", "sign in to sync"...</summary>
    public string Describe()
    {
        var state = Failure switch
        {
            null when Synced is null => "not synced yet",
            null => "synced",
            ServerFailure.Offline => "offline",
            ServerFailure.SignInNeeded => "sign in to sync",
            ServerFailure.Refused or ServerFailure.NotFound => "no longer on the server, or no longer shared with you",
            _ => "sync failed",
        };
        return Waiting > 0 ? $"{state}, {Waiting} {(Waiting == 1 ? "file" : "files")} to upload" : state;
    }
}

/// <summary>
/// Syncs the server vaults with their stores, in the background, one sync at a time: every 30
/// seconds, soon after a change (<see cref="Soon"/>), and when asked.
/// </summary>
public sealed class SyncService : IDisposable
{
    private const string Category = "Sync";
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(2);

    private readonly SettingsStore _settings;
    private readonly AppFiles _files;
    private readonly ServerConnection _server;
    private readonly ILog _log;
    private readonly DispatcherTimer _every = new() { Interval = Interval };
    private readonly DispatcherTimer _soon = new() { Interval = Settle };
    private readonly Dictionary<Guid, SyncStatus> _statuses = [];
    private Task? _running;
    private bool _again;

    public SyncService(SettingsStore settings, AppFiles files, ServerConnection server, ILog log)
    {
        _settings = settings;
        _files = files;
        _server = server;
        _log = log;
        _every.Tick += (_, _) => _ = SyncNowAsync();
        _soon.Tick += (_, _) =>
        {
            _soon.Stop();
            _ = SyncNowAsync();
        };
        server.Changed += Soon;
        _every.Start();
    }

    /// <summary>Raised on the UI thread when a sync ends.</summary>
    public event Action? Changed;

    public SyncStatus StatusOf(StoreAddress store) =>
        _statuses.TryGetValue(store.Id, out var status) ? status : new SyncStatus(null, null, 0);

    /// <summary>Syncs in a moment: changes come in bursts (a note and its content, a sync's own downloads).</summary>
    public void Soon()
    {
        _soon.Stop();
        _soon.Start();
    }

    /// <summary>Syncs every server vault now, or once more after the sync running ends.</summary>
    public Task SyncNowAsync()
    {
        if (_running is { IsCompleted: false } running)
        {
            _again = true;
            return running;
        }

        return _running = RunAsync();
    }

    /// <summary>Syncs one store now, apart from the others: a vault being added from the server.</summary>
    public async Task<SyncStatus> SyncAsync(StoreAddress store)
    {
        var status = _statuses[store.Id] = await RunOneAsync(store);
        Changed?.Invoke();
        return status;
    }

    public void Dispose()
    {
        _every.Stop();
        _soon.Stop();
    }

    private async Task RunAsync()
    {
        do
        {
            _again = false;
            await _server.RefreshAsync();
            foreach (var store in ServerVaults())
            {
                _statuses[store.Id] = await RunOneAsync(store);
            }

            Changed?.Invoke();
        }
        while (_again);
    }

    private IEnumerable<StoreAddress> ServerVaults() =>
        _settings.Vaults.Vaults.Select(vault => vault.Location.Store).OfType<StoreAddress>().ToList();

    private async Task<SyncStatus> RunOneAsync(StoreAddress store)
    {
        var (mirror, state) = (_files.MirrorOf(store.Id), _files.SyncStateOf(store.Id));
        var before = StatusOf(store);
        Task<int> Waiting() => Task.Run(() => ServerSync.Waiting(mirror, state));
        if (_server.Client is not { Session: not null } client || _server.Account?.Server != store.Server)
        {
            return new SyncStatus(before.Synced, ServerFailure.SignInNeeded, await Waiting());
        }

        try
        {
            await Task.Run(() => new ServerSync(mirror, state, store.Id, client, _log).RunAsync());

            // Read once, then kept: the owner whose signatures the vault's keys and members need.
            await _server.Owners.OwnerOfAsync(client, store.Id);
            return new SyncStatus(DateTimeOffset.Now, null, await Waiting());
        }
        catch (ServerException ex)
        {
            if (ex.Failure != before.Failure)
            {
                // Logged once, not every 30 seconds while offline.
                var level = ex.Failure == ServerFailure.Offline ? LogLevel.Info : LogLevel.Warning;
                _log.Write(LogEntry.Create(level, Category, $"Store {store.Id} not synced: {ex.Message}"));
            }

            return new SyncStatus(before.Synced, ex.Failure, await Waiting());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error(Category, $"Store {store.Id} not synced", ex);
            return new SyncStatus(before.Synced, ServerFailure.Failed, before.Waiting);
        }
    }
}
