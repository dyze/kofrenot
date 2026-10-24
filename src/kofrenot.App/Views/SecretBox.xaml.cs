using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Kofrenot.UI.Views;

/// <summary>
/// A <see cref="PasswordBox"/> whose eye button, while held, shows the text typed. The shown copy
/// is cleared once the button is released.
/// </summary>
public partial class SecretBox : UserControl
{
    public SecretBox() => InitializeComponent();

    public event RoutedEventHandler? PasswordChanged;

    public string Password => Box.Password;

    public void Clear() => Box.Clear();

    public void SelectAll() => Box.SelectAll();

    /// <summary>Puts the caret in the box: the control itself takes no focus.</summary>
    public new bool Focus() => Box.Focus();

    private bool IsRevealed => RevealedBox.Visibility == Visibility.Visible;

    private void Box_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (IsRevealed)
        {
            RevealedBox.Text = Box.Password;
        }

        PasswordChanged?.Invoke(this, e);
    }

    private void RevealButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        RevealedBox.Text = Box.Password;
        RevealedBox.Visibility = Visibility.Visible;
    }

    private void RevealButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => Hide();

    private void RevealButton_LostMouseCapture(object sender, MouseEventArgs e) => Hide();

    private void Hide()
    {
        RevealedBox.Visibility = Visibility.Collapsed;
        RevealedBox.Text = string.Empty;
    }
}
