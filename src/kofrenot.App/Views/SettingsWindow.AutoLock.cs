using System.Windows.Controls;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Settings;

namespace Kofrenot.UI.Views;

/// <summary>The General tab's hide or lock when Windows locks, and the cover and lock after a while without input.</summary>
public partial class SettingsWindow
{
    private static readonly Option<int>[] IdleOptions =
    [
        new(0, "Never"),
        new(1, "1 minute"),
        new(2, "2 minutes"),
        new(5, "5 minutes"),
        new(10, "10 minutes"),
        new(15, "15 minutes"),
        new(30, "30 minutes"),
        new(60, "1 hour"),
        new(240, "4 hours"),
    ];

    private static readonly Option<WindowsLockAction>[] OnWindowsLockOptions =
    [
        new(WindowsLockAction.Nothing, "Do nothing"),
        new(WindowsLockAction.Hide, "Hide the vault"),
        new(WindowsLockAction.Lock, "Lock the vault"),
    ];

    private bool _closingForLock;

    /// <summary>Closes Settings as the vault locks on its own: a node type being changed is not asked about, its changes are dropped.</summary>
    public void CloseForLock()
    {
        _closingForLock = true;
        Close();
    }

    private void ShowAutoLock()
    {
        OnWindowsLockComboBox.SelectedItem = OnWindowsLockOptions.First(option => option.Value == _settings.OnWindowsLock);
        CoverAfterComboBox.SelectedItem = IdleOptionOf(_settings.CoverAfterMinutes);
        LockAfterComboBox.SelectedItem = IdleOptionOf(_settings.LockAfterMinutes);
    }

    // Null, nothing selected, for a time not offered (written by hand in the file).
    private static Option<int>? IdleOptionOf(int minutes) => IdleOptions.FirstOrDefault(option => option.Value == minutes);

    private void OnWindowsLockComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || OnWindowsLockComboBox.SelectedItem is not Option<WindowsLockAction> option || option.Value == _settings.OnWindowsLock)
        {
            return;
        }

        ChangeAutoLock(() => _settings.SetOnWindowsLock(option.Value), $"When Windows locks: {option.Label}");
    }

    private void CoverAfterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ChangeIdle(CoverAfterComboBox, _settings.CoverAfterMinutes, _settings.SetCoverAfterMinutes, "Cover after");

    private void LockAfterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ChangeIdle(LockAfterComboBox, _settings.LockAfterMinutes, _settings.SetLockAfterMinutes, "Lock after");

    private void ChangeIdle(ComboBox box, int current, Action<int> set, string what)
    {
        if (_loading || box.SelectedItem is not Option<int> option || option.Value == current)
        {
            return;
        }

        ChangeAutoLock(() => set(option.Value), $"{what}: {option.Label} without input");
    }

    private void ChangeAutoLock(Action change, string done)
    {
        try
        {
            change();
            _log.Info("Settings", done);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _log.Error("Settings", "Cannot change when the vault is covered or locked", ex);
            ShowMessage(ex.Message);
            _loading = true;
            ShowAutoLock();
            _loading = false;
        }
    }
}
