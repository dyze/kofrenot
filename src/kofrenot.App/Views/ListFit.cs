using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kofrenot.UI.Views;

/// <summary>
/// A list of the Settings page that scrolls on its own (<c>local:ListFit.Fills="True"</c> on its
/// <see cref="ScrollViewer"/>): it takes the room the page leaves it, so what is above and below
/// it stays in view. While settings are searched, every tab is shown at once: each list then
/// keeps a fixed height.
/// </summary>
public static class ListFit
{
    // A short window still shows a few lines of the list.
    private const double MinHeight = 120;
    private const double SearchHeight = 200;
    private const double BottomGap = 8;

    public static readonly DependencyProperty FillsProperty =
        DependencyProperty.RegisterAttached("Fills", typeof(bool), typeof(ListFit), new PropertyMetadata(false));

    public static bool GetFills(DependencyObject element) => (bool)element.GetValue(FillsProperty);

    public static void SetFills(DependencyObject element, bool value) => element.SetValue(FillsProperty, value);

    /// <summary>Gives each visible list of <paramref name="page"/> its height.</summary>
    public static void FitAll(ScrollViewer page, bool searching)
    {
        foreach (var list in VisibleLists(page))
        {
            var height = searching ? SearchHeight : RoomLeft(list, page);
            if (Math.Abs(list.MaxHeight - height) > 0.5)
            {
                list.MaxHeight = height;
            }
        }
    }

    /// <summary>The page's height less what is above the list and what is below it.</summary>
    private static double RoomLeft(ScrollViewer list, ScrollViewer page)
    {
        var top = list.TranslatePoint(new Point(0, 0), page).Y + page.VerticalOffset;
        var below = page.ExtentHeight - top - list.ActualHeight;
        return Math.Max(MinHeight, page.ViewportHeight - top - below - BottomGap);
    }

    private static IEnumerable<ScrollViewer> VisibleLists(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is UIElement { IsVisible: false })
            {
                continue;
            }

            if (child is ScrollViewer list && GetFills(list))
            {
                yield return list;
            }

            foreach (var inner in VisibleLists(child))
            {
                yield return inner;
            }
        }
    }
}
