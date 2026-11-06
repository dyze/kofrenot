using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Kofrenot.Core;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Session;
using Kofrenot.UI.Platform;
using Microsoft.Win32;

namespace Kofrenot.UI.Views;

/// <summary>
/// The vault covered, hidden or locked on its own (Settings > General, <see cref="PrivacyGuard"/>):
/// hidden or locked when Windows locks or goes to sleep, covered then locked after the minutes
/// without input set.
/// A lock closes the dialogs open first, as Cancel; while a note is not saved or a file imported,
/// the vault stays covered and is locked once it can be.
/// </summary>
public partial class MainWindow
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LockBeforeSleep = TimeSpan.FromSeconds(2);

    private readonly PrivacyGuard _guard;

    private void AttachAutoLock()
    {
        var timer = new DispatcherTimer(CheckEvery, DispatcherPriority.Background, (_, _) => CheckPrivacy(), Dispatcher);
        timer.Start();

        // Raised on a thread of their own; detached at shutdown, as they are static.
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        Dispatcher.ShutdownStarted += (_, _) =>
        {
            timer.Stop();
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        };
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock)
        {
            Dispatcher.BeginInvoke(() => WindowsLocking("Windows locked"));
        }
    }

    // Waits for the lock, briefly: Windows goes to sleep once this returns.
    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            Dispatcher.Invoke(() => WindowsLocking("Windows goes to sleep"), DispatcherPriority.Send, CancellationToken.None, LockBeforeSleep);
        }
    }

    private void WindowsLocking(string why)
    {
        _guard.WindowsLocking(why);
        CheckPrivacy();
    }

    private void CheckPrivacy()
    {
        var shown = IsVisible && WindowState != WindowState.Minimized && !Covered;
        switch (_guard.Next(UserInput.IdleTime(), _session.Current is not null, shown))
        {
            case PrivacyStep.Cover:
                CoverNow("no input");
                break;
            case PrivacyStep.Hide:
                HideOnItsOwn();
                break;
            case PrivacyStep.Lock:
                LockOnItsOwn();
                break;
        }
    }

    /// <summary>Covers the vault, even with the privacy screen off, and hides the window; the dialogs open stay.</summary>
    private void HideOnItsOwn()
    {
        _log.Info("Privacy", "Vault hidden on its own");
        CoverNow("hide due");
        if (IsVisible)
        {
            DetailsPanel.Leave(ask: false);
            AfterDrawn(Hide);
        }
    }

    /// <summary>Covers the vault, closes the dialogs open, then locks it, unless a note is not saved or a file imported.</summary>
    private void LockOnItsOwn()
    {
        CoverNow("lock due");
        if (ComponentDispatcher.IsThreadModal)
        {
            // They return once this does: the next check locks.
            CloseDialogs(this);
            _guard.LockWaits("a dialog is open, closed first");
            return;
        }

        if (WhyNot(QuitRefusal.LockVault) is not null)
        {
            _guard.LockWaits("a note is not saved or a file is being imported");
            return;
        }

        _log.Info("Privacy", "Vault locked on its own");
        _session.Lock();
        Uncover();
    }

    // The innermost first, so each dialog returns before the one under it.
    private static void CloseDialogs(Window owner)
    {
        foreach (var dialog in owner.OwnedWindows.Cast<Window>().ToList())
        {
            CloseDialogs(dialog);
            if (dialog is SettingsWindow settings)
            {
                settings.CloseForLock();
            }
            else
            {
                dialog.Close();
            }
        }
    }
}
