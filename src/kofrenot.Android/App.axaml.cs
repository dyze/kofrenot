using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Kofrenot.Phone.Views;

namespace Kofrenot.Phone;

public sealed partial class App : Application
{
    private AppServices? _services;

    public static new App? Current => Application.Current as App;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
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
    public void Resumed() => _ = Services.Sync.ResumeAsync();

    /// <summary>The app went to the background: no sync until it comes back.</summary>
    public void Paused() => Services.Sync.Pause();

    private AppServices Services => _services ??= new AppServices(global::Android.App.Application.Context.FilesDir!.AbsolutePath);
}
