using System.Windows;
using System.Windows.Interop;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>Asks before deleting a server vault: its name typed again, and the account's password.</summary>
public partial class DeleteServerVaultDialog : Window
{
    private readonly string _name;

    private DeleteServerVaultDialog(Window owner, string name)
    {
        InitializeComponent();
        Owner = owner;
        _name = name;
        HintText.Text = $"“{name}” goes to the server's trash, for every device: the server deletes it for good after a while (30 days by default). Type its name to confirm.";
        Loaded += (_, _) => NameBox.Focus();
    }

    /// <summary>The password typed, once the name matches; null when cancelled.</summary>
    public static string? Ask(Window owner, string name)
    {
        var dialog = new DeleteServerVaultDialog(owner, name);
        return dialog.ShowDialog() == true ? dialog.PasswordBox.Password : null;
    }

    private void NameBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ShowDeletable();

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e) => ShowDeletable();

    private void ShowDeletable() => DeleteButton.IsEnabled = NameBox.Text.Trim() == _name && PasswordBox.Password.Length > 0;

    private void DeleteButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new WindowInteropHelper(this).Handle);
}
