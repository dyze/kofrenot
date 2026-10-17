using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>
/// Asks the pass phrase of a vault: to unlock it, or (twice) to create it. The pass phrase is
/// handed to <c>accept</c> in the background, as deriving the key takes a moment; the dialog
/// stays open while it is refused.
/// </summary>
public partial class PassphraseDialog : Window
{
    private readonly bool _create;
    private readonly Func<string, bool> _accept;

    private PassphraseDialog(Window owner, bool create, string folder, Func<string, bool> accept)
    {
        InitializeComponent();
        Owner = owner;
        _create = create;
        _accept = accept;

        TitleText.Text = create ? "New vault" : "Unlock vault";
        FolderText.Text = folder;
        FolderText.ToolTip = folder;
        HintText.Text = create
            ? "Choose a pass phrase. Everything in the vault is encrypted with it: if it is lost, the vault cannot be read."
            : "Type the pass phrase of this vault.";
        SecondPanel.Visibility = create ? Visibility.Visible : Visibility.Collapsed;
        OkButton.Content = create ? "Create" : "Unlock";
        Loaded += (_, _) => FirstBox.Focus();
    }

    /// <summary>
    /// Shows the dialog until <paramref name="accept"/> takes a pass phrase (true) or the user
    /// cancels. Returns whether to remember it, or null when cancelled.
    /// </summary>
    /// <param name="accept">Creates or unlocks the vault; false for a wrong pass phrase. Runs in the background.</param>
    public static bool? Ask(Window owner, bool create, string folder, Func<string, bool> accept)
    {
        var dialog = new PassphraseDialog(owner, create, folder, accept);
        return dialog.ShowDialog() == true ? dialog.RememberCheckBox.IsChecked == true : null;
    }

    private async void OkButton_Click(object sender, RoutedEventArgs e)
    {
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
