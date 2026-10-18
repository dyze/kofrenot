using System.Windows;
using System.Windows.Interop;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>A message in the app's theme, centered on its owner (the Windows <c>MessageBox</c> ignores the theme).</summary>
public partial class MessageDialog : Window
{
    /// <param name="owner">The window to center on; null before any window (at start): centered on the screen, in the taskbar.</param>
    private MessageDialog(Window? owner, string message)
    {
        InitializeComponent();
        Owner = owner;
        if (owner is null)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = true;
        }

        MessageText.Text = message;
        Loaded += (_, _) => OkButton.Focus();
    }

    public static void Inform(Window owner, string message) =>
        new MessageDialog(owner, message).ShowDialog();

    /// <summary>Asks a question: true when the user chose <paramref name="yes"/>.</summary>
    public static bool Confirm(Window? owner, string message, string yes, string no = "Cancel")
    {
        var dialog = new MessageDialog(owner, message);
        dialog.OkButton.Content = yes;
        dialog.OkButton.IsCancel = false;
        dialog.CancelButton.Content = no;
        dialog.CancelButton.IsCancel = true;
        dialog.CancelButton.Visibility = Visibility.Visible;
        return dialog.ShowDialog() == true;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new WindowInteropHelper(this).Handle);
}
