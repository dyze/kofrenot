using System.Windows;
using System.Windows.Interop;
using Kofrenot.Core;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Placement;
using Kofrenot.Core.Server;
using Kofrenot.Core.Session;
using Kofrenot.Core.Settings;
using Kofrenot.Core.Sharing;
using Kofrenot.Core.Storage;
using Kofrenot.Core.Updates;
using Kofrenot.UI.Logging;
using Kofrenot.UI.Platform;
using Kofrenot.UI.Tray;
using Kofrenot.UI.Views;

namespace Kofrenot.UI;

public partial class App : Application
{
    private LogHub? _logHub;
    private SingleInstance? _singleInstance;
    private MainWindow? _window;
    private LogWindow? _logWindow;
    private TrayIcon? _tray;
    private VaultSession? _session;
    private ServerConnection? _server;
    private SyncService? _sync;
    private GlobalHotkey? _hotkey;

    // How long the copy running may take to quit when this one replaces it.
    private static readonly TimeSpan ReplaceTimeout = TimeSpan.FromSeconds(10);

    // Set at startup; a closed window cannot be shown again, so each opening creates a new one.
    private Func<LogWindow>? _createLogWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var files = AppFiles.For(
            AppContext.BaseDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            arguments: e.Args);
        var settings = new SettingsStore(files.Settings, files.MirrorOf);

        // Before any window (the dialogs about another copy too) or the tray menu is created.
        AppTheme.ApplyToTrayMenu(settings.Theme);
        AppTheme.Apply(settings.Theme);

        _singleInstance = ClaimSingleInstance(out var replaced);
        if (_singleInstance is null)
        {
            Shutdown(0);
            return;
        }

        _logHub = new LogHub(files.Logs, settings.DebugLogging);
        var log = _logHub.Filter;
        var version = AppVersion.Of(typeof(App).Assembly);
        log.Info("App", $"kofrenot {AppVersion.WithCommitOf(typeof(App).Assembly)} started"
            + (files.IsPortable ? " (portable)" : string.Empty)
            + (files.IsChosen ? $" (data in {files.DataDirectory})" : string.Empty)
            + (settings.DebugLogging ? " (debug logging on)" : string.Empty));
        if (replaced is not null)
        {
            log.Info("App", $"Replaced {replaced.Describe()}");
        }

        log.Debug("App", $"Theme: {settings.Theme}");

        DispatcherUnhandledException += (_, args) =>
            log.Error("App", "Unhandled error", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            log.Error("App", "Fatal error: " + args.ExceptionObject);

        var autoStart = new RegistryAutoStart(Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "kofrenot.exe"));
        var placer = new WindowPlacer(new PlacementStore(files.WindowPositions), log);
        var updates = new UpdateChecker(new GitHubReleaseFeed(version), new UpdateStore(files.UpdateCheck), version, log: log);
        var logHub = _logHub;
        var protector = new DpapiProtector();
        var ui = new DispatcherUiThread();
        var server = _server = new ServerConnection(settings, new ServerSessionMemory(files.ServerSession, protector, log), new AccountIdentities(files.AccountIdentity, log), new VaultOwners(files.VaultOwners, log), ui, log, exchanges: new ExchangeStats(files.ExchangeStats, log: log));
        var sync = _sync = new SyncService(settings, files, server, ui, log);
        var session = _session = new VaultSession(settings, files, new KeyMemory(files.RememberedKeys, protector, log), new VaultNameMemory(files.VaultNames, protector, log), server, sync, ui, new VaultWatching(), new WpfThumbnailMaker(), log);
        session.Changed += sync.Soon;
        var opened = new OpenedFiles(files.Opened, log);
        opened.Clear();
        _window = new MainWindow(
            settings,
            log,
            updates,
            session,
            new RecentNodes(files.RecentNodes, log),
            new ViewTabs(files.ViewTabs, log),
            new ViewStates(files.ViewStates, log),
            opened,
            createSettings: () =>
            {
                var created = new SettingsWindow(settings, logHub, files, updates, session, () => _window?.WhyNotSwitch(), wanted => ApplyHotkey(wanted, settings.Hotkey), (vault, wanted) => ApplyVaultHotkey(settings, vault, wanted, log), autoStart, openLog: OpenLogWindow);
                placer.Attach(created, "Settings", RestoreMode.SizeOverOwner);
                return created;
            },
            createImageWindow: () =>
            {
                var created = new ImageWindow { Log = log };
                placer.Attach(created, "Image", RestoreMode.PositionAndSize);
                return created;
            });
        var window = _window;
        placer.Attach(window, "Main", RestoreMode.PositionAndSize);

        var hotkey = _hotkey = new GlobalHotkey(window, id =>
        {
            if (_vaultHotkeys.TryGetValue(id, out var vault))
            {
                log.Debug("Action", "Vault hotkey pressed");
                window.ToggleVault(vault);
                return;
            }

            log.Debug("Action", "Hotkey pressed");
            window.ShowFromOutside();
        });
        if (!hotkey.Register(MainHotkeyId, settings.Hotkey))
        {
            log.Warn("App", $"Hotkey {settings.Hotkey} is used by another program: change it in Settings > General");
        }

        RegisterVaultHotkeys(settings, log);
        session.Changed += () => RegisterVaultHotkeys(settings, log);

        _createLogWindow = () =>
        {
            var created = new LogWindow(logHub.Ring, logHub.Directory);
            placer.Attach(created, "Log", RestoreMode.PositionAndSize, created.Columns);
            return created;
        };

        window.CheckForUpdates();

        _tray = new TrayIcon(
            version,
            show: () => window.ShowFromOutside(),
            // After the menu has closed: Settings is a dialog, it waits until closed.
            showSettings: () => Dispatcher.BeginInvoke(() => window.ShowSettings()),
            isLogOpen: () => _logWindow is not null,
            toggleLog: ToggleLogWindow,
            // After the menu has closed: a refusal shows a dialog.
            quit: () => Dispatcher.BeginInvoke(() => TryQuit("Quit from the tray")));

        // Once the window is on screen, so the pass phrase dialog has an owner to center on.
        void OpenVault() => Dispatcher.BeginInvoke(() => session.Open(new WpfQuestions(window)), System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        // Started by the user or by the installer: show the window. At sign-in, stay in the tray,
        // and open the vault (which may ask for its pass phrase) only when the window is first shown.
        if (AutoStartCommand.StartsHidden(e.Args))
        {
            log.Debug("App", "Started hidden");
            DependencyPropertyChangedEventHandler? firstShown = null;
            firstShown = (_, _) =>
            {
                window.IsVisibleChanged -= firstShown;
                OpenVault();
            };
            window.IsVisibleChanged += firstShown;
        }
        else
        {
            window.ShowFromOutside();
            OpenVault();
        }
    }

    /// <summary>
    /// The instance guard when this copy runs; null when it leaves. Another copy running: the same
    /// copy (started again) shows its window. Another one (installed, portable or a build) is named,
    /// and closed for this one to start (it is then <paramref name="replaced"/>) or shown, as the
    /// user chooses. A copy of 0.3 or older cannot be asked to close: the user quits it.
    /// </summary>
    private SingleInstance? ClaimSingleInstance(out AppCopy? replaced)
    {
        replaced = null;
        if (SingleInstance.TryClaim(OnShowRequested, OnQuitRequested) is { } claimed)
        {
            return claimed;
        }

        var starting = new AppCopy(Environment.ProcessPath ?? AppContext.BaseDirectory, AppVersion.BuildOf(typeof(App).Assembly) ?? string.Empty);
        if (SingleInstance.FindRunning() is not { } running || running.IsSameAs(starting))
        {
            SingleInstance.ShowRunning();
            return null;
        }

        if (!SingleInstance.CanAskToQuit())
        {
            if (MessageDialog.Confirm(null, RunningCopy.Warning(starting, running), "Show the running one", "Close"))
            {
                SingleInstance.ShowRunning();
            }

            return null;
        }

        if (!MessageDialog.Confirm(null, RunningCopy.ReplaceQuestion(starting, running), "Close it, start this one", "Show the running one"))
        {
            SingleInstance.ShowRunning();
            return null;
        }

        var (guard, outcome) = SingleInstance.TryReplace(OnShowRequested, OnQuitRequested, ReplaceTimeout);
        switch (outcome)
        {
            case ReplaceOutcome.Replaced:
                replaced = running;
                break;
            case ReplaceOutcome.Refused:
                MessageDialog.Inform(null, RunningCopy.Refused);
                break;
            case ReplaceOutcome.NotClosed:
                MessageDialog.Inform(null, RunningCopy.NotClosed);
                break;
        }

        return guard;
    }

    // Called on a background thread by SingleInstance.
    private void OnShowRequested() => Dispatcher.BeginInvoke(() => _window?.ShowFromOutside());

    private void OnQuitRequested() => Dispatcher.BeginInvoke(() =>
        TryQuit("Quit: replaced by another kofrenot", onRefused: () => _singleInstance?.RefuseQuit()));

    /// <summary>The tray's Log window item: closes the log window when it is open, else opens it.</summary>
    private void ToggleLogWindow()
    {
        if (_logWindow is { } open)
        {
            _logHub?.Filter.Info("Action", "Log window closed from the tray");
            open.Close();
            return;
        }

        _logHub?.Filter.Info("Action", "Log window opened from the tray");
        OpenLogWindow();
    }

    /// <summary>Opens the log window, or brings it to the front when it is already open.</summary>
    /// <param name="over">
    /// A modal window that stays open (Settings over the main window): the log window is shown in
    /// front of it and stays usable while it is open, then stays open on its own.
    /// </param>
    private void OpenLogWindow(Window? over = null)
    {
        if (_logWindow is null && _createLogWindow is not null)
        {
            _logWindow = _createLogWindow();
            _logWindow.Closed += (_, _) => _logWindow = null;
        }

        if (_logWindow is not { } logWindow)
        {
            return;
        }

        // In front of the dialog, but not always on top: the windows then stack as usual.
        BringToFront(logWindow);

        // A log window opened before Settings was disabled by it, as a modal dialog does with the others.
        if (over is not null)
        {
            NativeWindows.Enable(new WindowInteropHelper(logWindow).Handle);
        }
    }

    private static void BringToFront(Window window)
    {
        if (!window.IsVisible)
        {
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }

    /// <summary>
    /// Quits, unless a note is not saved or a file is being imported: the window then shows why,
    /// after <paramref name="onRefused"/> (called before the message, which waits for the user).
    /// </summary>
    private void TryQuit(string action, Action? onRefused = null)
    {
        var log = _logHub!.Filter;
        if (_window?.WhyNotQuit() is not { } reason)
        {
            log.Info("Action", action);
            Shutdown();
            return;
        }

        log.Info("Action", $"{action}: refused, a note is not saved or a file is being imported");
        onRefused?.Invoke();
        _window.ShowFromOutside();
        MessageDialog.Inform(_window, reason);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _hotkey?.Dispose();
        _session?.Dispose();
        _sync?.Dispose();
        _server?.Dispose();
        _logHub?.Filter.Info("App", "kofrenot stopped");
        _logHub?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
