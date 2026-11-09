using Avalonia;
using Avalonia.Controls;

namespace Kofrenot.Phone.Views;

/// <summary>A row that scrolls across (the labels of a typed box, the trail): its right end brought into sight.</summary>
public static class HorizontalScroll
{
    /// <summary>Scrolled to its right end; <see cref="ScrollViewer.ScrollToEnd"/> scrolls down, not across.</summary>
    public static void ToRight(ScrollViewer scroller) =>
        scroller.Offset = new Vector(Math.Max(0, scroller.Extent.Width - scroller.Viewport.Width), 0);

    /// <summary>Kept at its right end each time its content or its width changes, once laid out.</summary>
    public static void KeepRight(ScrollViewer scroller) =>
        scroller.PropertyChanged += (_, e) =>
        {
            if (e.Property == ScrollViewer.ExtentProperty || e.Property == ScrollViewer.ViewportProperty)
            {
                ToRight(scroller);
            }
        };
}
