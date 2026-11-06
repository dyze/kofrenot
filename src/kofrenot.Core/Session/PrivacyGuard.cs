using Kofrenot.Core.Logging;
using Kofrenot.Core.Settings;

namespace Kofrenot.Core.Session;

/// <summary>What the window does to the open vault now, without being asked.</summary>
public enum PrivacyStep
{
    None,

    /// <summary>Covers the vault shown: Show uncovers it, no pass phrase asked.</summary>
    Cover,

    /// <summary>Covers the vault and hides the window: shown again, it opens on the cover.</summary>
    Hide,

    /// <summary>Locks the vault: its pass phrase is asked, even when remembered.</summary>
    Lock,
}

/// <summary>
/// When the open vault is covered, hidden or locked on its own (Settings > General): hidden or
/// locked when Windows locks or goes to sleep, covered then locked after the minutes without input
/// set. A lock stays due until the vault is closed: while it cannot be locked (a note not saved, a
/// file imported), the window asks again. A hide is done once.
/// </summary>
public sealed class PrivacyGuard(SettingsStore settings, ILog? log = null)
{
    private const string Category = "Privacy";

    private readonly ILog _log = log ?? NullLog.Instance;
    private bool _lockDue;
    private bool _hideDue;
    private bool _waitTold;

    /// <summary>Windows locks the session or goes to sleep (<paramref name="why"/>): the vault is due to be hidden or locked, as set.</summary>
    public void WindowsLocking(string why)
    {
        switch (settings.OnWindowsLock)
        {
            case WindowsLockAction.Lock:
                _lockDue = true;
                break;
            case WindowsLockAction.Hide:
                _hideDue = true;
                break;
            default:
                return;
        }

        _log.Debug(Category, $"{settings.OnWindowsLock} due: {why}");
    }

    /// <summary>What to do now.</summary>
    /// <param name="idle">The time since the last keyboard or mouse input.</param>
    /// <param name="vaultOpen">A vault is open (not locked, not closed).</param>
    /// <param name="vaultShown">The vault is on screen: the window shown, not minimized, not covered.</param>
    public PrivacyStep Next(TimeSpan idle, bool vaultOpen, bool vaultShown)
    {
        if (!vaultOpen)
        {
            (_lockDue, _hideDue, _waitTold) = (false, false, false);
            return PrivacyStep.None;
        }

        if (_lockDue || Elapsed(settings.LockAfterMinutes, idle))
        {
            _hideDue = false;
            return PrivacyStep.Lock;
        }

        _waitTold = false;
        if (_hideDue)
        {
            _hideDue = false;
            return PrivacyStep.Hide;
        }

        return vaultShown && Elapsed(settings.CoverAfterMinutes, idle) ? PrivacyStep.Cover : PrivacyStep.None;
    }

    /// <summary>The lock due cannot be done yet (<paramref name="why"/>): told once, until the vault is locked or the lock no longer due.</summary>
    public void LockWaits(string why)
    {
        if (!_waitTold)
        {
            _waitTold = true;
            _log.Warn(Category, $"The vault cannot be locked yet, covered until then: {why}");
        }
    }

    private static bool Elapsed(int minutes, TimeSpan idle) => minutes > 0 && idle >= TimeSpan.FromMinutes(minutes);
}
