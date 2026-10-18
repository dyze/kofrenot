using System.Windows;
using System.Windows.Interop;
using Kofrenot.Core;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Placement;
using Kofrenot.Core.Settings;
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
    private GlobalHotkey? _hotkey;

    // Set at startup; a closed window cannot be shown again, so each opening creates a new one.
    private Func<LogWindow>? _createLogWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var files = AppFiles.For(
            AppContext.BaseDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        var settings = new SettingsStore(files.Settings);

        // Another copy is running: this one leaves, after showing it or warning about it.
        _singleInstance = SingleInstance.TryClaim(() => Dispatcher.BeginInvoke(() => _window?.ShowFromOutside()));
        if (_singleInstance is null)
        {
            AppTheme.Apply(settings.Theme);
            ShowOrWarnAboutRunningCopy();
            Shutdown(0);
            return;
        }

        // Before any window or the tray menu is created.
        AppTheme.ApplyToTrayMenu(settings.Theme);
        AppTheme.Apply(settings.Theme);
        _logHub = new LogHub(files.Logs, settings.DebugLogging);
        var log = _logHub.Filter;
        var version = AppVersion.Of(typeof(App).Assembly);
        log.Info("App", $"kofrenot {version} started"
            + (files.IsPortable ? " (portable)" : string.Empty)
            + (settings.DebugLogging ? " (debug logging on)" : string.Empty));
        log.Debug("App", $"Theme: {settings.Theme}");

        DispatcherUnhandledException += (_, args) =>
            log.Error("App", "Unhandled error", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            log.Error("App", "Fatal error: " + args.ExceptionObject);

        var placer = new WindowPlacer(new PlacementStore(files.WindowPositions), log);
        var updates = new UpdateChecker(new GitHubReleaseFeed(version), new UpdateStore(files.UpdateCheck), version, log: log);
        var logHub = _logHub;
        var session = _session = new VaultSession(settings, files, new KeyMemory(files.RememberedKeys, new DpapiProtector(), log), log);
        _window = new MainWindow(
            settings,
            log,
            updates,
            session,
            new RecentNodes(files.RecentNodes, log),
            new ViewTabs(files.ViewTabs, log),
            createSettings: () =>
            {
                var created = new SettingsWindow(settings, logHub, files, updates, session, wanted => ApplyHotkey(wanted, settings.Hotkey), openLog: OpenLogWindow);
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

        var hotkey = _hotkey = new GlobalHotkey(window, () =>
        {
            log.Debug("Action", "Hotkey pressed");
            window.ShowFromOutside();
        });
        if (!hotkey.Register(settings.Hotkey))
        {
            log.Warn("App", $"Hotkey {settings.Hotkey} is used by another program: change it in Settings > General");
        }

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
            quit: () =>
            {
                log.Info("Action", "Quit from the tray");
                Shutdown();
            });

        window.ShowFromOutside();

        // Once the window is on screen, so the pass phrase dialog has an owner to center on.
        Dispatcher.BeginInvoke(() => session.Open(window), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// The same copy is running (started again): it shows its window. Another copy (installed,
    /// portable or a build): this one says which, instead of silently showing the other. Nothing
    /// is stopped; the user may still show the running one.
    /// </summary>
    private static void ShowOrWarnAboutRunningCopy()
    {
        var starting = new AppCopy(Environment.ProcessPath ?? AppContext.BaseDirectory, AppVersion.Of(typeof(App).Assembly));
        if (SingleInstance.FindRunning() is not { } running || running.IsSameAs(starting)
            || MessageDialog.Confirm(null, RunningCopy.Warning(starting, running), "Show the running one", "Close"))
        {
            SingleInstance.ShowRunning();
        }
    }

    /// <summary>Registers a new hotkey. One that another program already uses: <paramref name="current"/> is registered again.</summary>
    private bool ApplyHotkey(Hotkey wanted, Hotkey current)
    {
        if (_hotkey is null || _hotkey.Register(wanted))
        {
            return _hotkey is not null;
        }

        _hotkey.Register(current);
        return false;
    }

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

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _hotkey?.Dispose();
        _session?.Dispose();
        _logHub?.Filter.Info("App", "kofrenot stopped");
        _logHub?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
