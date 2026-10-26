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
            activity.MainViewFactory = () => new Shell(Services);
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
        {
            single.MainView = new Shell(Services);
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

    private AppServices Services => _services ??= ServicesOf(global::Android.App.Application.Context);

    private static AppServices ServicesOf(global::Android.Content.Context context) =>
        new(context.FilesDir!.AbsolutePath, new OpenedFiles(context));
}
