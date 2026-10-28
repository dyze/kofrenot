using Kofrenot.Core;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Server;
using Kofrenot.Core.Session;
using Kofrenot.Core.Settings;
using Kofrenot.Core.Sharing;
using Kofrenot.Core.Storage;
using Kofrenot.Core.Views;
using Kofrenot.Phone.Platform;
using Kofrenot.UI.Logging;

namespace Kofrenot.Phone;

/// <summary>
/// What kofrenot holds on the phone, wired once: its files in the app's private folder, the log,
/// the server account, the syncs, the session, which edits notes and files only, and the files
/// opened with other apps and the photos and videos taken, deleted at each start.
/// </summary>
public sealed class AppServices : IDisposable
{
    public AppServices(string dataDirectory, AppChooser chooser, PickedFiles picked, CameraCapture camera)
    {
        Files = new AppFiles(dataDirectory, AppContext.BaseDirectory);
        Settings = new SettingsStore(Files.Settings, Files.MirrorOf);
        Logs = new LogHub(Files.Logs, Settings.DebugLogging);
        var log = Log;
        var protector = new KeystoreProtector();
        var ui = new AvaloniaUiThread();
        Server = new ServerConnection(Settings, new ServerSessionMemory(Files.ServerSession, protector, log), new AccountIdentities(Files.AccountIdentity, log), new VaultOwners(Files.VaultOwners, log), ui, log);
        Sync = new SyncService(Settings, Files, Server, ui, log);
        Session = new VaultSession(Settings, Files, new KeyMemory(Files.RememberedKeys, protector, log), new VaultNameMemory(Files.VaultNames, protector, log), Server, Sync, ui, new NothingToWatch(), new AndroidThumbnailMaker(), log, mode: ClientMode.Content);
        Recent = new RecentNodes(Files.RecentNodes, log);
        ViewTabs = new ViewTabs(Files.ViewTabs, log);
        log.Info("App", $"kofrenot {Version} for Android started");
        Chooser = chooser;
        Picked = picked;
        Opened = new OpenedFiles(chooser.Folder, log);
        Opened.Clear();
        Camera = camera;
        Captures = new OpenedFiles(camera.Folder, log);
        Captures.Clear();
    }

    /// <summary>The version with its commit and the commit's date, for Settings > About and the log.</summary>
    public static string Version => AppVersion.WithCommitOf(typeof(AppServices).Assembly);

    public AppFiles Files { get; }

    public SettingsStore Settings { get; }

    public LogHub Logs { get; }

    public ILog Log => Logs.Filter;

    public ServerConnection Server { get; }

    public SyncService Sync { get; }

    public VaultSession Session { get; }

    public RecentNodes Recent { get; }

    public ViewTabs ViewTabs { get; }

    public OpenedFiles Opened { get; }

    public AppChooser Chooser { get; }

    public PickedFiles Picked { get; }

    public CameraCapture Camera { get; }

    /// <summary>The photos and videos taken, until they are added (or at the next start).</summary>
    public OpenedFiles Captures { get; }

    public void Dispose()
    {
        Session.Dispose();
        Sync.Dispose();
        Server.Dispose();
        Logs.Dispose();
    }
}
