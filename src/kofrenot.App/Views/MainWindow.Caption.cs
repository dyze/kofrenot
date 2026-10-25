using System.Windows;

namespace Kofrenot.UI.Views;

/// <summary>
/// The title bar, drawn by kofrenot (<see cref="System.Windows.Shell.WindowChrome"/>): the vault box
/// or the vault's name, its lock and sync, and Settings, beside Windows' own buttons.
/// </summary>
public partial class MainWindow
{
    public const double CaptionHeight = 40;

    public static readonly GridLength CaptionHeightLength = new(CaptionHeight);

    /// <summary>The room of Windows' own buttons (minimize, maximize, close), 46 each, kept free of controls.</summary>
    public static readonly GridLength CaptionButtonsWidth = new(3 * 46);

    /// <summary>
    /// A maximized window with its own title bar reaches past the screen by its frame (the resize
    /// border and the padded border, as wide): its content is moved back in.
    /// </summary>
    private void FitMaximized()
    {
        var frame = SystemParameters.WindowResizeBorderThickness;
        Root.Margin = WindowState == WindowState.Maximized
            ? new Thickness(2 * frame.Left, 2 * frame.Top, 2 * frame.Right, 2 * frame.Bottom)
            : new Thickness(0);
    }

    /// <summary>The vault's name in the title bar when the vault box is not shown (one vault), or kofrenot without any.</summary>
    private void ShowTitle()
    {
        TitleText.Text = SelectedName.Length > 0 ? SelectedName : "kofrenot";
        TitleText.Visibility = VaultBox.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    }
}
