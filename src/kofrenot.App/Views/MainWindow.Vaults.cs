using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Settings;

namespace Kofrenot.UI.Views;

/// <summary>The vault box: the registered vaults, shown when there are several; choosing one switches to it.</summary>
public partial class MainWindow
{
    // Set while the box is filled, so its selection is not taken as the user's choice.
    private bool _showingVaults;

    /// <summary>The name of the selected vault, for the footer; empty when none is registered.</summary>
    private string SelectedName => _session.Selected is { } selected ? _session.NameOf(selected) : string.Empty;

    /// <summary>Fills the vault box with the registered vaults, the selected one chosen. Left alone while it is dropped down.</summary>
    private void ShowVaults()
    {
        var vaults = _session.Vaults;
        VaultBox.Visibility = vaults.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        ShowTitle();
        if (vaults.Count <= 1 || VaultBox.IsDropDownOpen)
        {
            return;
        }

        _showingVaults = true;
        try
        {
            var choices = vaults.Select(vault => new VaultChoice(vault, vault.IsLocked ? $"🔒 {_session.NameOf(vault)}" : _session.NameOf(vault))).ToList();
            VaultBox.ItemsSource = choices;
            VaultBox.SelectedItem = choices.FirstOrDefault(choice => choice.Vault.Location == _session.Selected?.Location);
        }
        finally
        {
            _showingVaults = false;
        }
    }

    /// <summary>Marks the vaults whose folder cannot be reached, checked off the UI thread (a file share may be slow to answer).</summary>
    private async void VaultBox_DropDownOpened(object? sender, EventArgs e)
    {
        if (VaultBox.ItemsSource is not IEnumerable<VaultChoice> choices)
        {
            return;
        }

        await Task.WhenAll(choices.Select(async choice => choice.SetAvailable(await Task.Run(() => Directory.Exists(choice.Folder)))));
    }

    private void VaultBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_showingVaults || VaultBox.SelectedItem is not VaultChoice { Vault: var target } || target.Location == _session.Selected?.Location)
        {
            return;
        }

        // Once the drop-down has closed: switching may ask a pass phrase or say why it cannot.
        Dispatcher.BeginInvoke(() => SwitchTo(target.Location));
    }

    /// <summary>Ctrl+L locks the vault; Ctrl+1 to Ctrl+9 switch to the vault at that place in the list. False for another key.</summary>
    private bool HandleVaultKey(Key key)
    {
        if (key == Key.L)
        {
            Lock();
            return true;
        }

        var place = key switch
        {
            >= Key.D1 and <= Key.D9 => key - Key.D1,
            >= Key.NumPad1 and <= Key.NumPad9 => key - Key.NumPad1,
            _ => -1,
        };
        if (place < 0 || place >= _session.Vaults.Count)
        {
            return false;
        }

        if (_session.Vaults[place].Location != _session.Selected?.Location)
        {
            _log.Info("Action", $"Switch vaults with Ctrl+{place + 1}");
            SwitchTo(_session.Vaults[place].Location);
        }

        return true;
    }

    private void LockButton_Click(object sender, RoutedEventArgs e) => Lock();

    /// <summary>Locks the open vault: closed, still selected; the state panel offers to unlock it. Not while <see cref="WhyNotSwitch"/>.</summary>
    private void Lock()
    {
        if (_session.Current is null)
        {
            return;
        }

        if (WhyNot(QuitRefusal.LockVault) is { } reason)
        {
            MessageDialog.Inform(this, reason);
            return;
        }

        _session.Lock();
    }

    /// <summary>
    /// A vault's hotkey: shows this window on that vault, switching to it; hides the window when
    /// it is in front showing that vault already.
    /// </summary>
    public void ToggleVault(VaultLocation target)
    {
        if (IsVisible && IsActive && WindowState != WindowState.Minimized && _settingsDialog is null && target == _session.Selected?.Location)
        {
            DetailsPanel.LeaveNote();
            HideCovered();
            _log.Debug("Window", "Hidden (vault hotkey)");
            return;
        }

        ShowFromOutside();
        if (_settingsDialog is null && target != _session.Selected?.Location)
        {
            SwitchTo(target);
        }
    }

    /// <summary>
    /// Why the open vault cannot be closed for another now: a file is being imported, or the note
    /// shown could not be saved (it is saved first, as when the window is hidden). Null otherwise.
    /// </summary>
    public string? WhyNotSwitch() => WhyNot(QuitRefusal.SwitchVaults);

    private string? WhyNot(string action)
    {
        DetailsPanel.LeaveNote();
        return QuitRefusal.Reason(DetailsPanel.UnsavedNote, _session.ImportsRunning, action);
    }

    /// <summary>Switches to another registered vault, unless <see cref="WhyNotSwitch"/>.</summary>
    private void SwitchTo(VaultLocation target)
    {
        if (WhyNotSwitch() is { } reason)
        {
            _log.Info("Action", "Switch vaults: refused, a note is not saved or a file is being imported");
            ShowVaults();
            MessageDialog.Inform(this, reason);
            return;
        }

        _session.SwitchTo(Ask, target);
        ShowVaults();
        FocusTab();
    }
}
