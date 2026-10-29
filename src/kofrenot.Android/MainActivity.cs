using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia.Android;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone;

// One instance: a share from another app reaches the running app (OnNewIntent).
[Activity(
    Label = "kofrenot",
    Theme = "@style/Kofrenot.Theme",
    Icon = "@drawable/icon",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
[IntentFilter([Intent.ActionSend, Intent.ActionSendMultiple], Categories = [Intent.CategoryDefault], DataMimeType = "*/*")]
public sealed class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // The vault's content stays out of the recent apps list and of screenshots.
        Window?.SetFlags(WindowManagerFlags.Secure, WindowManagerFlags.Secure);
        base.OnCreate(savedInstanceState);
        App.Current?.Shown(this);
        // The camera app is done. Avalonia's activity overrides OnActivityResult already (its file picker):
        // overriding it again here leaves .NET Android without its native methods, and the app without a start.
        ActivityResult += (requestCode, resultCode, _) => App.Current?.ActivityFinished(requestCode, resultCode);
        Received(Intent);
    }

    protected override void OnDestroy()
    {
        App.Current?.Shown(null);
        base.OnDestroy();
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Received(intent);
    }

    // Files or text shared by another app: added once a vault is open, after their preview.
    private void Received(Intent? intent)
    {
        if (SharedContent.Of(this, intent) is { } shared)
        {
            App.Current?.Shared(shared);
        }
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
