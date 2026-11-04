using System.Windows;
using Kofrenot.Core.Datasets;

namespace Kofrenot.UI.Views;

/// <summary>
/// A Paste button shown only while the clipboard holds what it takes (<see cref="PasteOffer"/>):
/// followed while it is on screen, so a button gone does not keep the clipboard's event.
/// </summary>
internal static class PasteButtons
{
    /// <param name="isOn">Whether the button is wanted at all, the clipboard aside (a vault open, not read only).</param>
    /// <returns>Shows or hides the button again: to call when what <paramref name="isOn"/> reads changes.</returns>
    public static Action Follow(FrameworkElement button, IClipboard clipboard, Func<ClipboardContent, bool> fits, Func<bool>? isOn = null)
    {
        var following = false;
        void Show() => button.Visibility = (isOn?.Invoke() ?? true) && fits(clipboard.Content) ? Visibility.Visible : Visibility.Collapsed;
        button.Loaded += (_, _) =>
        {
            if (!following)
            {
                following = true;
                clipboard.Changed += Show;
            }

            Show();
        };
        button.Unloaded += (_, _) =>
        {
            following = false;
            clipboard.Changed -= Show;
        };
        Show();
        return Show;
    }
}
