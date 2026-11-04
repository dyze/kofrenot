using System.Windows;
using System.Windows.Controls;

namespace Kofrenot.UI.Views;

/// <summary>
/// Children of one width, each as tall as its content, in columns: each goes under the shortest
/// column (the leftmost of equals), so cards of different heights leave no gaps. As many columns as
/// the width holds, at least one.
/// </summary>
public sealed class MasonryPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var columnWidth = 0.0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            columnWidth = Math.Max(columnWidth, child.DesiredSize.Width);
        }

        var columns = Place(availableSize.Width, columnWidth, (_, _, _) => { });
        return new Size(columnWidth * columns.Length, columns.DefaultIfEmpty(0).Max());
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columnWidth = InternalChildren.Cast<UIElement>().Select(child => child.DesiredSize.Width).DefaultIfEmpty(0).Max();
        Place(finalSize.Width, columnWidth, (child, x, y) => child.Arrange(new Rect(x, y, columnWidth, child.DesiredSize.Height)));
        return finalSize;
    }

    /// <summary>Puts each child under the shortest column (<paramref name="place"/>), giving each column's height.</summary>
    private double[] Place(double width, double columnWidth, Action<UIElement, double, double> place)
    {
        var count = columnWidth <= 0 || double.IsInfinity(width) ? 1 : Math.Max(1, (int)(width / columnWidth));
        var heights = new double[count];
        foreach (UIElement child in InternalChildren)
        {
            var shortest = Array.IndexOf(heights, heights.Min());
            place(child, shortest * columnWidth, heights[shortest]);
            heights[shortest] += child.DesiredSize.Height;
        }

        return heights;
    }
}
