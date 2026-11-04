using System.Windows;
using System.Windows.Controls;

namespace Kofrenot.UI.Views;

/// <summary>
/// Its children left to right, the first given the width the others leave: the tabs, then ▾ and +
/// right after them, never pushed out of the window.
/// </summary>
public sealed class TabBarPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var children = InternalChildren.Cast<UIElement>().ToList();
        if (children.Count == 0)
        {
            return default;
        }

        foreach (var trailing in children.Skip(1))
        {
            trailing.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        }

        var trailingWidth = children.Skip(1).Sum(child => child.DesiredSize.Width);
        children[0].Measure(new Size(Math.Max(0, availableSize.Width - trailingWidth), availableSize.Height));
        return new Size(children.Sum(child => child.DesiredSize.Width), children.Max(child => child.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        foreach (UIElement child in InternalChildren)
        {
            child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
            x += child.DesiredSize.Width;
        }

        return finalSize;
    }
}
