using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Kofrenot.Core.Logging;
using Kofrenot.Phone.Platform;
using Kofrenot.Phone.Views;

namespace Kofrenot.Phone;

public sealed partial class App : Application
{
    private AppServices? _services;
    private Shell? _shell;

    public static new App? Current => Application.Current as App;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // The last resort: a failure nothing caught is logged, not a crash.
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Services.Log.Error("App", $"Unexpected failure ({e.Exception.GetType().Name})", e.Exception);
            e.Handled = true;
        };

        if (ApplicationLifetime is IActivityApplicationLifetime activity)
        {
            activity.MainViewFactory = () => _shell = new Shell(Services);
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
        {
            single.MainView = _shell = new Shell(Services);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>The app came back on screen: syncs now, then every 30 s.</summary>
    public async void Resumed()
    {
        try
        {
            await Services.Sync.ResumeAsync();
        }
        catch (Exception ex)
        {
            Services.Log.Error("Sync", $"Sync on resume failed ({ex.GetType().Name})", ex);
        }
    }

    /// <summary>The app went to the background: no sync until it comes back.</summary>
    public void Paused() => Services.Sync.Pause();

    /// <summary>What another app shared, waiting until a vault is open (<see cref="Shell.AddShared"/>).</summary>
    public SharedContent? WaitingShare { get; private set; }

    /// <summary>Another app shared files or text: added once a vault is open, after their preview.</summary>
    public void Shared(SharedContent shared)
    {
        Services.Log.Info("Action", shared.Text is null ? $"Files shared from another app ({shared.Files.Count})" : "Text shared from another app");
        WaitingShare = shared;
        _shell?.AddShared();
    }

    /// <summary>The share waiting, taken to be added; null when none.</summary>
    public SharedContent? TakeShare()
    {
        var shared = WaitingShare;
        WaitingShare = null;
        return shared;
    }

    private AppServices Services => _services ??= ServicesOf(global::Android.App.Application.Context);

    private static AppServices ServicesOf(global::Android.Content.Context context) =>
        new(context.FilesDir!.AbsolutePath, new AppChooser(context), new PickedFiles(context), new CameraCapture(context));

    /// <summary>The activity shown, or none: what starts the camera app, and whose screens the privacy screen keeps private.</summary>
    public void Shown(global::Android.App.Activity? activity)
    {
        Services.Camera.Activity = activity;
        ApplyPrivacyScreen();
    }

    /// <summary>
    /// The privacy screen (Settings > General), on by default: the app's screens stay out of screenshots
    /// and of the recent apps list (FLAG_SECURE); off, both show them.
    /// </summary>
    public void ApplyPrivacyScreen()
    {
        if (Services.Camera.Activity?.Window is not { } window)
        {
            return;
        }

        if (Services.Settings.PrivacyScreen)
        {
            window.AddFlags(global::Android.Views.WindowManagerFlags.Secure);
        }
        else
        {
            window.ClearFlags(global::Android.Views.WindowManagerFlags.Secure);
        }
    }

    /// <summary>An app started for a result is done (the camera app).</summary>
    public void ActivityFinished(int requestCode, global::Android.App.Result result) => Services.Camera.Finished(requestCode, result);
}
