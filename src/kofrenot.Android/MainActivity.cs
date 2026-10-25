using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia.Android;

namespace Kofrenot.Phone;

[Activity(
    Label = "kofrenot",
    Theme = "@style/Kofrenot.Theme",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // The vault's content stays out of the recent apps list and of screenshots.
        Window?.SetFlags(WindowManagerFlags.Secure, WindowManagerFlags.Secure);
        base.OnCreate(savedInstanceState);
    }

    // Syncs only while shown: when it comes back, then every 30 s.
    protected override void OnResume()
    {
        base.OnResume();
        App.Current?.Resumed();
    }

    protected override void OnPause()
    {
        App.Current?.Paused();
        base.OnPause();
    }
}
