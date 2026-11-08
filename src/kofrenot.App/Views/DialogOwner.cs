using System.Windows;

namespace Kofrenot.UI.Views;

/// <summary>
/// The window a dialog opens over. An action awaiting the server can outlive the window it started
/// from (Settings closed meanwhile), and WPF refuses a closed window as owner: the main window then
/// owns the dialog.
/// </summary>
internal static class DialogOwner
{
    public static Window Of(Window window) =>
        IsOpen(window) ? window : Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault(IsOpen) ?? window;

    // A closed window has lost its Win32 window; a hidden one keeps it.
    private static bool IsOpen(Window window) => PresentationSource.FromVisual(window) is not null;
}
