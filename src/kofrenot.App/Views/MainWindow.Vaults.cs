using System.IO;
using System.Windows;
using System.Windows.Controls;
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
        if (vaults.Count <= 1 || VaultBox.IsDropDownOpen)
        {
            return;
        }

        _showingVaults = true;
        try
        {
            var choices = vaults.Select(vault => new VaultChoice(vault, _session.NameOf(vault))).ToList();
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

    /// <summary>
    /// Switches to another registered vault, the note shown saved first (as when the window is
    /// hidden); not while a file is being imported, or a note could not be saved.
    /// </summary>
    private void SwitchTo(VaultLocation target)
    {
        DetailsPanel.LeaveNote();
        if (QuitRefusal.Reason(DetailsPanel.UnsavedNote, _session.ImportsRunning, QuitRefusal.SwitchVaults) is { } reason)
        {
            _log.Info("Action", "Switch vaults: refused, a note is not saved or a file is being imported");
            ShowVaults();
            MessageDialog.Inform(this, reason);
            return;
        }

        _session.SwitchTo(this, target);
        ShowVaults();
        FocusTab();
    }
}
