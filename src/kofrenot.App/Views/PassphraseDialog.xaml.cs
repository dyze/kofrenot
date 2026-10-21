using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>What the user chose for a new vault: its name, and whether to remember its pass phrase.</summary>
public sealed record NewVault(string Name, bool Remember);

/// <summary>
/// Asks the pass phrase of a vault: to unlock it, or (twice, with its name) to create it. The
/// pass phrase is handed to <c>accept</c> in the background, as deriving the key takes a moment;
/// the dialog stays open while it is refused.
/// </summary>
public partial class PassphraseDialog : Window
{
    private readonly bool _create;
    private readonly Func<string, bool> _accept;

    /// <param name="newName">The name offered for a new vault; null to unlock one.</param>
    private PassphraseDialog(Window owner, string place, string? newName, Func<string, bool> accept)
    {
        InitializeComponent();
        Owner = owner;
        _create = newName is not null;
        _accept = accept;

        TitleText.Text = _create ? "New vault" : "Unlock vault";
        FolderText.Text = place;
        FolderText.ToolTip = place;
        HintText.Text = _create
            ? "Name it, and choose a pass phrase. Everything in the vault is encrypted with it: if it is lost, the vault cannot be read."
            : "Type the pass phrase of this vault.";
        NamePanel.Visibility = _create ? Visibility.Visible : Visibility.Collapsed;
        NameBox.Text = newName ?? string.Empty;
        SecondPanel.Visibility = _create ? Visibility.Visible : Visibility.Collapsed;
        OkButton.Content = _create ? "Create" : "Unlock";
        Loaded += (_, _) =>
        {
            if (_create && NameBox.Text.Length == 0)
            {
                NameBox.Focus();
            }
            else
            {
                FirstBox.Focus();
            }
        };
    }

    /// <summary>
    /// Asks the pass phrase of a vault until <paramref name="accept"/> takes it (true) or the user
    /// cancels. Returns whether to remember it, or null when cancelled.
    /// </summary>
    /// <param name="place">Where the vault is: its folder.</param>
    /// <param name="accept">Unlocks the vault; false for a wrong pass phrase. Runs in the background.</param>
    public static bool? Ask(Window owner, string place, Func<string, bool> accept)
    {
        var dialog = new PassphraseDialog(owner, place, newName: null, accept);
        return dialog.ShowDialog() == true ? dialog.RememberCheckBox.IsChecked == true : null;
    }

    /// <summary>
    /// Asks the name and pass phrase of a new vault until <paramref name="accept"/> creates it
    /// (true) or the user cancels. Null when cancelled.
    /// </summary>
    /// <param name="place">Where the vault will be: its folder, or its server.</param>
    /// <param name="name">The name offered, changed at will; empty to have one typed.</param>
    /// <param name="accept">Creates the vault with the pass phrase. Runs in the background.</param>
    public static NewVault? AskNew(Window owner, string place, string name, Func<string, bool> accept)
    {
        var dialog = new PassphraseDialog(owner, place, name, accept);
        return dialog.ShowDialog() == true ? new NewVault(dialog.NameBox.Text.Trim(), dialog.RememberCheckBox.IsChecked == true) : null;
    }

    private async void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (_create && NameBox.Text.Trim().Length == 0)
        {
            ShowError("Name the vault: every device shows it by this name.");
            NameBox.Focus();
            return;
        }

        var passPhrase = FirstBox.Password;
        if (passPhrase.Length == 0)
        {
            ShowError("Type a pass phrase.");
            return;
        }

        if (_create && passPhrase != SecondBox.Password)
        {
            ShowError("The two pass phrases differ.");
            return;
        }

        IsEnabled = false;
        Cursor = Cursors.Wait;
        string? error;
        try
        {
            error = await Task.Run(() => _accept(passPhrase)) ? null : "Wrong pass phrase.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
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
        FirstBox.Focus();
        FirstBox.SelectAll();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new WindowInteropHelper(this).Handle);
}
