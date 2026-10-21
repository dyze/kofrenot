using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Kofrenot.Core.Storage;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>
/// The recovery key of a vault: shown once, after its creation or upgrade, and closed only once
/// the user says it is saved; or typed, to unlock the vault when its pass phrase is lost.
/// </summary>
public partial class RecoveryKeyDialog : Window
{
    private readonly Func<RecoveryKey, bool>? _accept;

    /// <param name="shown">The key to show; null to have one typed, handed to <paramref name="accept"/>.</param>
    private RecoveryKeyDialog(Window owner, string place, RecoveryKey? shown, Func<RecoveryKey, bool>? accept)
    {
        InitializeComponent();
        Owner = owner;
        _accept = accept;
        FolderText.Text = place;
        FolderText.ToolTip = place;
        var show = shown is not null;
        TitleText.Text = show ? "Recovery key" : "Unlock with the recovery key";
        HintText.Text = show
            ? "If the pass phrase is lost, this key opens the vault. It is shown only now: write it down or keep it in a password manager, away from this computer."
            : "Type the recovery key shown when the vault was created or upgraded. Case, spaces and dashes do not matter.";
        KeyBox.Text = shown?.ToString() ?? string.Empty;
        KeyBox.IsReadOnly = show;
        CopyButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        SavedCheckBox.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        RememberCheckBox.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        CancelButton.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        OkButton.Content = show ? "Done" : "Unlock";
        OkButton.IsEnabled = !show;
        Loaded += (_, _) => KeyBox.Focus();
    }

    private bool IsShowing => _accept is null;

    /// <summary>Shows <paramref name="key"/> until the user says it is saved.</summary>
    /// <param name="place">Where the vault is: its folder, or its server.</param>
    public static void Show(Window owner, string place, RecoveryKey key) =>
        new RecoveryKeyDialog(owner, place, key, accept: null).ShowDialog();

    /// <summary>
    /// Asks the recovery key until <paramref name="accept"/> takes it (true) or the user cancels.
    /// Returns whether to remember what it opens, or null when cancelled.
    /// </summary>
    /// <param name="accept">Unlocks the vault; false when the key is not its. Runs in the background.</param>
    public static bool? Ask(Window owner, string place, Func<RecoveryKey, bool> accept)
    {
        var dialog = new RecoveryKeyDialog(owner, place, shown: null, accept);
        return dialog.ShowDialog() == true ? dialog.RememberCheckBox.IsChecked == true : null;
    }

    private async void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (_accept is null)
        {
            DialogResult = true;
            return;
        }

        if (RecoveryKey.Parse(KeyBox.Text) is not { } key)
        {
            ShowError("This is not a recovery key: 52 letters and digits, in groups of 4.");
            return;
        }

        IsEnabled = false;
        Cursor = Cursors.Wait;
        string? error;
        try
        {
            error = await Task.Run(() => _accept(key)) ? null : "This recovery key does not open the vault.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            error = ex.Message;
        }
        finally
        {
            IsEnabled = true;
            Cursor = null;
        }

        if (error is null)
        {
            DialogResult = true;
            return;
        }

        ShowError(error);
        KeyBox.Focus();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(KeyBox.Text);

    private void SavedCheckBox_Changed(object sender, RoutedEventArgs e) => OkButton.IsEnabled = SavedCheckBox.IsChecked == true;

    // A shown key is closed only once saved: it is never shown again.
    private void Window_Closing(object? sender, CancelEventArgs e) =>
        e.Cancel = IsShowing && SavedCheckBox.IsChecked != true;

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new WindowInteropHelper(this).Handle);
}
