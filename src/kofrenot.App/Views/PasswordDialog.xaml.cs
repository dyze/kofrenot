using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>
/// Asks the server account's current password and a new one (twice), then hands them to
/// <c>change</c>, which returns why the server refused (null: changed). Stays open while refused.
/// </summary>
public partial class PasswordDialog : Window
{
    public const int MinLength = 10;

    private readonly Func<string, string, Task<string?>> _change;

    private PasswordDialog(Window owner, string place, Func<string, string, Task<string?>> change)
    {
        InitializeComponent();
        Owner = owner;
        _change = change;
        PlaceText.Text = place;
        Loaded += (_, _) => CurrentBox.Focus();
    }

    /// <summary>True once <paramref name="change"/> changed the password; false when cancelled.</summary>
    public static bool Ask(Window owner, string place, Func<string, string, Task<string?>> change) =>
        new PasswordDialog(owner, place, change).ShowDialog() == true;

    private async void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentBox.Password.Length == 0)
        {
            ShowError("Type your current password.");
            return;
        }

        if (NewBox.Password.Length < MinLength)
        {
            ShowError($"The new password needs at least {MinLength} characters.");
            return;
        }

        if (NewBox.Password != AgainBox.Password)
        {
            ShowError("The two new passwords differ.");
            return;
        }

        IsEnabled = false;
        Cursor = Cursors.Wait;
        string? error;
        try
        {
            error = await _change(CurrentBox.Password, NewBox.Password);
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
        CurrentBox.Focus();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new WindowInteropHelper(this).Handle);
}
