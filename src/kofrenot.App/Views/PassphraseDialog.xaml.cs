using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Kofrenot.Core.Session;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>
/// Asks the pass phrase of a vault: to unlock or upgrade it, or (twice) to create it, with its
/// name, or to change it. The pass phrase is handed to <c>accept</c> in the background, as deriving
/// the key takes a moment; the dialog stays open while it is refused.
/// </summary>
public partial class PassphraseDialog : Window
{
    private readonly Mode _mode;
    private readonly Func<string, bool> _accept;
    private bool _useRecoveryKey;

    private const string AccountRole = "It opens all your server vaults; a recovery key, shown next, opens them too. If both are lost, they cannot be read.";

    private PassphraseDialog(Window owner, Mode mode, string place, Func<string, bool> accept, string? newName = null, string? hint = null)
    {
        InitializeComponent();
        Owner = owner;
        _mode = mode;
        _accept = accept;

        (TitleText.Text, HintText.Text, OkButton.Content) = mode switch
        {
            Mode.Create => ("New vault", "Name it, and choose a pass phrase. Everything in the vault is encrypted with it; a recovery key, shown next, opens it too. If both are lost, the vault cannot be read.", "Create"),
            Mode.CreateOnAccount => ("New vault", "Name it, and type your server account's pass phrase: it opens the vault.", "Create"),
            Mode.CreateWithNewAccount => ("New vault", $"Name it, and choose your server account's pass phrase. {AccountRole}", "Create"),
            Mode.Upgrade => ("Upgrade vault", "Type the pass phrase of this vault: the upgrade seals its key with it, and makes a recovery key, shown next.", "Upgrade"),
            Mode.Change => ("New pass phrase", "Choose the pass phrase that unlocks this vault from now on. The recovery key stays the same.", "Change"),
            Mode.Account => ("Server account", hint ?? "Type your server account's pass phrase.", "Continue"),
            Mode.NewAccount => ("Server account", $"{hint} Choose your server account's pass phrase. {AccountRole}", "Continue"),
            Mode.ChangeAccount => ("New pass phrase", "Choose your server account's pass phrase from now on: it opens all your server vaults. The recovery key stays the same.", "Change"),
            _ => ("Unlock vault", hint ?? "Type the pass phrase of this vault.", "Unlock"),
        };
        FolderText.Text = place;
        FolderText.ToolTip = place;
        NamePanel.Visibility = HasName ? Visibility.Visible : Visibility.Collapsed;
        NameBox.Text = newName ?? string.Empty;
        FirstLabel.Text = mode is Mode.Change or Mode.ChangeAccount ? "New pass phrase" : "Pass phrase";
        SecondPanel.Visibility = IsNew ? Visibility.Visible : Visibility.Collapsed;
        RememberCheckBox.Visibility = mode == Mode.Unlock || HasName ? Visibility.Visible : Visibility.Collapsed;
        RecoveryButton.Visibility = mode == Mode.Unlock ? Visibility.Visible : Visibility.Collapsed;
        Loaded += (_, _) =>
        {
            if (HasName && NameBox.Text.Length == 0)
            {
                NameBox.Focus();
            }
            else
            {
                FirstBox.Focus();
            }
        };
    }

    private enum Mode
    {
        Unlock,
        Create,
        CreateOnAccount,
        CreateWithNewAccount,
        Upgrade,
        Change,
        Account,
        NewAccount,
        ChangeAccount,
    }

    private bool HasName => _mode is Mode.Create or Mode.CreateOnAccount or Mode.CreateWithNewAccount;

    // A new pass phrase is typed twice.
    private bool IsNew => _mode is Mode.Create or Mode.CreateWithNewAccount or Mode.Change or Mode.NewAccount or Mode.ChangeAccount;

    /// <summary>
    /// Asks the pass phrase of a vault until <paramref name="accept"/> takes it (true), the user
    /// chooses the recovery key instead, or cancels (null).
    /// </summary>
    /// <param name="place">Where the vault is: its folder.</param>
    /// <param name="accept">Unlocks the vault; false for a wrong pass phrase. Runs in the background.</param>
    /// <param name="hint">What to type, when not the vault's own pass phrase.</param>
    public static UnlockAnswer? Ask(Window owner, string place, Func<string, bool> accept, string? hint = null)
    {
        var dialog = new PassphraseDialog(owner, Mode.Unlock, place, accept, hint: hint);
        return dialog.ShowDialog() == true ? new UnlockAnswer(dialog.RememberCheckBox.IsChecked == true, dialog._useRecoveryKey) : null;
    }

    /// <summary>
    /// Asks the name and pass phrase of a new vault until <paramref name="accept"/> creates it
    /// (true) or the user cancels. Null when cancelled.
    /// </summary>
    /// <param name="place">Where the vault will be: its folder, or its server.</param>
    /// <param name="name">The name offered, changed at will; empty to have one typed.</param>
    /// <param name="accept">Creates the vault with the pass phrase. Runs in the background.</param>
    public static NewVault? AskNew(Window owner, string place, string name, Func<string, bool> accept) =>
        AskNew(owner, Mode.Create, place, name, accept);

    /// <summary>As <see cref="AskNew(Window, string, string, Func{string, bool})"/>, for a server vault: the account's pass phrase, chosen when it has none yet.</summary>
    public static NewVault? AskNewOnServer(Window owner, string place, bool accountHasPassPhrase, Func<string, bool> accept) =>
        AskNew(owner, accountHasPassPhrase ? Mode.CreateOnAccount : Mode.CreateWithNewAccount, place, string.Empty, accept);

    /// <summary>Asks the pass phrase of a vault to upgrade until <paramref name="accept"/> takes it (true) or the user cancels (false).</summary>
    public static bool AskToUpgrade(Window owner, string place, Func<string, bool> accept) =>
        new PassphraseDialog(owner, Mode.Upgrade, place, accept).ShowDialog() == true;

    /// <summary>Asks a new pass phrase, twice, until <paramref name="accept"/> sets it (true) or the user cancels (false).</summary>
    public static bool AskToChange(Window owner, string place, Func<string, bool> accept) =>
        new PassphraseDialog(owner, Mode.Change, place, accept).ShowDialog() == true;

    /// <summary>
    /// Asks the server account's pass phrase until <paramref name="accept"/> takes it (true) or the
    /// user cancels (false): once to unlock it, twice to choose it when it has none yet.
    /// </summary>
    /// <param name="hint">Why it is asked.</param>
    public static bool AskAccount(Window owner, string place, bool exists, string hint, Func<string, bool> accept) =>
        new PassphraseDialog(owner, exists ? Mode.Account : Mode.NewAccount, place, accept, hint: hint).ShowDialog() == true;

    /// <summary>Asks a new pass phrase for the server account, twice, until <paramref name="accept"/> sets it (true) or the user cancels (false).</summary>
    public static bool AskToChangeAccount(Window owner, string place, Func<string, bool> accept) =>
        new PassphraseDialog(owner, Mode.ChangeAccount, place, accept).ShowDialog() == true;

    private static NewVault? AskNew(Window owner, Mode mode, string place, string name, Func<string, bool> accept)
    {
        var dialog = new PassphraseDialog(owner, mode, place, accept, name);
        return dialog.ShowDialog() == true ? new NewVault(dialog.NameBox.Text.Trim(), dialog.RememberCheckBox.IsChecked == true) : null;
    }

    private async void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (HasName && NameBox.Text.Trim().Length == 0)
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

        if (IsNew && passPhrase != SecondBox.Password)
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
        FirstBox.Focus();
        FirstBox.SelectAll();
    }

    private void RecoveryButton_Click(object sender, RoutedEventArgs e)
    {
        _useRecoveryKey = true;
        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new WindowInteropHelper(this).Handle);
}
