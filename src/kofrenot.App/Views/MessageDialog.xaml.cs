using System.Windows;
using System.Windows.Interop;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>A message in the app's theme, centered on its owner (the Windows <c>MessageBox</c> ignores the theme).</summary>
public partial class MessageDialog : Window
{
    private MessageDialog(Window owner, string message)
    {
        InitializeComponent();
        Owner = owner;
        MessageText.Text = message;
        Loaded += (_, _) => OkButton.Focus();
    }

    public static void Inform(Window owner, string message) =>
        new MessageDialog(owner, message).ShowDialog();

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new WindowInteropHelper(this).Handle);
}
