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
/// the server account, the syncs (downloads only), the session, which only reads, and the files
/// opened with other apps, deleted at each start.
/// </summary>
public sealed class AppServices : IDisposable
{
    public AppServices(string dataDirectory, AppChooser chooser)
    {
        Files = new AppFiles(dataDirectory, AppContext.BaseDirectory);
        Settings = new SettingsStore(Files.Settings, Files.MirrorOf);
        Logs = new LogHub(Files.Logs, Settings.DebugLogging);
        var log = Log;
        var protector = new KeystoreProtector();
        var ui = new AvaloniaUiThread();
        Server = new ServerConnection(Settings, new ServerSessionMemory(Files.ServerSession, protector, log), new AccountIdentities(Files.AccountIdentity, log), new VaultOwners(Files.VaultOwners, log), ui, log);
        Sync = new SyncService(Settings, Files, Server, ui, log, downloadsOnly: true);
        Session = new VaultSession(Settings, Files, new KeyMemory(Files.RememberedKeys, protector, log), new VaultNameMemory(Files.VaultNames, protector, log), Server, Sync, ui, new NothingToWatch(), new NoThumbnails(), log);
        Recent = new RecentNodes(Files.RecentNodes, log);
        ViewTabs = new ViewTabs(Files.ViewTabs, log);
        log.Info("App", $"kofrenot {Version} for Android started");
        Chooser = chooser;
        Opened = new OpenedFiles(chooser.Folder, log);
        Opened.Clear();
    }

    public static string Version => AppVersion.Of(typeof(AppServices).Assembly);

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

    public void Dispose()
    {
        Session.Dispose();
        Sync.Dispose();
        Server.Dispose();
        Logs.Dispose();
    }
}
