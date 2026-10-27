using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Views;

/// <summary>
/// The privacy screen (Settings > General, on by default): an open vault is covered when the window
/// is hidden or minimized, before it goes, so neither the window shown again nor Windows' pictures
/// of it (taskbar thumbnail, Alt+Tab) show the vault; Show uncovers it. Unlike the lock, the vault
/// stays unlocked.
/// </summary>
public partial class MainWindow
{
    private const int WmSysCommand = 0x0112;
    private const int ScMinimize = 0xF020;

    private bool Covered => PrivacyCover.Visibility == Visibility.Visible;

    private void AttachPrivacyScreen()
    {
        SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(OnMinimizing);
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized)
            {
                Cover();
            }
        };
    }

    /// <summary>Hides the window; an open vault is covered first, and the window hidden once the cover is drawn.</summary>
    private void HideCovered()
    {
        if (Cover())
        {
            AfterDrawn(Hide);
        }
        else
        {
            Hide();
        }
    }

    // A minimize (title bar, taskbar, Win+Down) waits for the cover to be drawn too.
    private IntPtr OnMinimizing(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmSysCommand && (wParam.ToInt64() & 0xFFF0) == ScMinimize && Cover())
        {
            handled = true;
            AfterDrawn(() => WindowState = WindowState.Minimized);
        }

        return IntPtr.Zero;
    }

    // Below the render priority: runs once the window is drawn.
    private void AfterDrawn(Action action) => Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, action);

    /// <summary>Covers an open vault, when the privacy screen is on; true when it was not covered yet. The image window closes.</summary>
    private bool Cover()
    {
        if (Covered || !_settings.PrivacyScreen || _session.Current is null)
        {
            return false;
        }

        MainArea.Visibility = Visibility.Hidden;
        PrivacyCover.Visibility = Visibility.Visible;
        _imageWindow?.Close();
        _log.Debug("Window", "Covered (privacy screen)");
        return true;
    }

    private void PrivacyShowButton_Click(object sender, RoutedEventArgs e)
    {
        _log.Info("Action", "Show the vault (privacy screen)");
        PrivacyCover.Visibility = Visibility.Collapsed;
        MainArea.Visibility = Visibility.Visible;
        FocusWork();
    }
}
