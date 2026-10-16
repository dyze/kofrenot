using System.Windows;
using System.Windows.Interop;
using Kofrenot.Core;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Placement;
using Kofrenot.Core.Settings;
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

    // Set at startup; a closed window cannot be shown again, so each opening creates a new one.
    private Func<LogWindow>? _createLogWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Another copy is running: it has been asked to show its window, so this one leaves.
        _singleInstance = SingleInstance.TryClaim(() => Dispatcher.BeginInvoke(() => _window?.ShowFromOutside()));
        if (_singleInstance is null)
        {
            Shutdown(0);
            return;
        }

        var files = AppFiles.For(
            AppContext.BaseDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        var settings = new SettingsStore(files.Settings);

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
        _window = new MainWindow(
            settings,
            log,
            updates,
            createSettings: () =>
            {
                var created = new SettingsWindow(settings, logHub, files, updates, openLog: OpenLogWindow);
                placer.Attach(created, "Settings", RestoreMode.SizeOverOwner);
                return created;
            });
        var window = _window;
        placer.Attach(window, "Main", RestoreMode.PositionAndSize);

        _createLogWindow = () =>
        {
            var created = new LogWindow(logHub.Ring, logHub.Directory);
            placer.Attach(created, "Log", RestoreMode.PositionAndSize);
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
    /// front of it and usable until it closes, then it stays open on its own.
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

        if (over is not null)
        {
            // In front of the dialog while it is open.
            logWindow.Topmost = true;
            over.Closed += (_, _) => logWindow.Topmost = false;
        }

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
        _logHub?.Filter.Info("App", "kofrenot stopped");
        _logHub?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
