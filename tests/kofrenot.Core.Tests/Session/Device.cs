using Kofrenot.Core.Sharing;
using Kofrenot.Core.Tests.Server;

namespace Kofrenot.Core.Tests.Session;

/// <summary>
/// One kofrenot in a test: its own data folder, its session over <see cref="FakeKofre"/>, a UI
/// thread and timers run by hand, and the user's answers scripted (<see cref="Ask"/>).
/// </summary>
internal sealed class Device : IDisposable
{
    public const string ServerPassword = "server password";

    private readonly TempFolder _temp = new();

    /// <param name="mode">What it may change: content only for kofrenot for Android.</param>
    public Device(FakeKofre kofre, ClientMode mode = ClientMode.Full)
    {
        Files = new AppFiles(_temp.Sub("data"), _temp.Sub("program"));
        Settings = new SettingsStore(Files.Settings);
        var protector = new PlainProtector();
        Server = new ServerConnection(
            Settings,
            new ServerSessionMemory(Files.ServerSession, protector, Log),
            new AccountIdentities(Files.AccountIdentity, Log),
            new VaultOwners(Files.VaultOwners, Log),
            Ui,
            Log,
            kofre);
        Sync = new SyncService(Settings, Files, Server, Ui, Log, Timers);
        Session = new VaultSession(
            Settings,
            Files,
            new KeyMemory(Files.RememberedKeys, protector, Log),
            new VaultNameMemory(Files.VaultNames, protector, Log),
            Server,
            Sync,
            Ui,
            Watching,
            new NoThumbnails(),
            Log,
            keyIterations: 10,
            time: Timers,
            mode: mode);
    }

    public AppFiles Files { get; }

    public SettingsStore Settings { get; }

    public RingLog Log { get; } = new();

    public QueuedUiThread Ui { get; } = new();

    public ManualTimers Timers { get; } = new();

    public FakeWatching Watching { get; } = new();

    public ScriptedQuestions Ask { get; } = new();

    public ServerConnection Server { get; }

    public SyncService Sync { get; }

    public VaultSession Session { get; }

    public Vault Vault => Session.Current ?? throw new InvalidOperationException("No vault open");

    /// <summary>A folder of this device's own, not created.</summary>
    public string Folder(string name) => _temp.Sub(name);

    /// <summary>Signs up (unless the account exists) and in to <see cref="FakeKofre"/>.</summary>
    public async Task SignInAsync(string email = "ada@example.com")
    {
        try
        {
            await Server.SignUpAsync(FakeKofre.Address, email, ServerPassword);
        }
        catch (ServerException ex) when (ex.Failure == ServerFailure.Conflict)
        {
            // Signed up on another device.
        }

        await Server.SignInAsync(FakeKofre.Address, email, ServerPassword);
        Ui.RunAll();
    }

    /// <summary>Syncs every server vault, then runs what it posted (the session's after-sync).</summary>
    public async Task SyncAsync()
    {
        await Sync.SyncNowAsync();
        Ui.RunAll();
    }

    /// <summary>A new folder vault, registered and open.</summary>
    public string CreateFolderVault(string name = "vault")
    {
        var folder = Folder(name);
        Directory.CreateDirectory(folder);
        Assert.Null(Session.Register(Ask, folder));
        Assert.NotNull(Session.Current);
        return folder;
    }

    public void Dispose()
    {
        Session.Dispose();
        Sync.Dispose();
        Server.Dispose();
        _temp.Dispose();
    }

    private sealed class PlainProtector : ISecretProtector
    {
        public byte[] Protect(byte[] secret) => secret;

        public byte[] Unprotect(byte[] protectedSecret) => protectedSecret;
    }

    private sealed class NoThumbnails : IThumbnailMaker
    {
        public byte[]? Make(Stream image) => null;
    }
}

/// <summary>Watches nothing: <see cref="Changed"/> stands for files arriving.</summary>
internal sealed class FakeWatching : IVaultWatcher
{
    private Action? _changed;

    public List<string> Folders { get; } = [];

    /// <summary>The folder cannot be watched (a file share that refuses it).</summary>
    public bool Fails { get; set; }

    public IDisposable Watch(string folder, Action changed)
    {
        if (Fails)
        {
            throw new IOException("Cannot watch");
        }

        Folders.Add(folder);
        _changed = changed;
        return new Stop(() => _changed = null);
    }

    public void Changed() => _changed?.Invoke();

    private sealed class Stop(Action stop) : IDisposable
    {
        public void Dispose() => stop();
    }
}
