using System.Windows;
using System.Windows.Controls;

namespace Kofrenot.UI.Views;

/// <summary>
/// A node's fields in as many columns as the width holds (each at least <see cref="MinColumnWidth"/>),
/// at least one: down the first column, then the next, so reading and Tab go down; the columns
/// balanced by height.
/// </summary>
public sealed class FieldColumnsPanel : Panel
{
    public const double MinColumnWidth = 580;
    public const double ColumnGap = 24;

    private double _columnWidth;
    private int[] _columnOf = [];

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = double.IsInfinity(availableSize.Width) ? 1 : Math.Max(1, (int)((availableSize.Width + ColumnGap) / (MinColumnWidth + ColumnGap)));
        _columnWidth = double.IsInfinity(availableSize.Width) ? MinColumnWidth : (availableSize.Width - (ColumnGap * (count - 1))) / count;
        var children = InternalChildren.Cast<UIElement>().ToList();
        foreach (var child in children)
        {
            child.Measure(new Size(_columnWidth, double.PositiveInfinity));
        }

        _columnOf = Split(children.Select(child => child.DesiredSize.Height).ToList(), count);
        var heights = new double[count];
        var widest = new double[count];
        for (var index = 0; index < children.Count; index++)
        {
            heights[_columnOf[index]] += children[index].DesiredSize.Height;
            widest[_columnOf[index]] = Math.Max(widest[_columnOf[index]], children[index].DesiredSize.Width);
        }

        var used = _columnOf.DefaultIfEmpty(0).Max() + 1;
        var width = used == 1 ? widest[0] : ((_columnWidth + ColumnGap) * (used - 1)) + widest[used - 1];
        return new Size(width, heights.DefaultIfEmpty(0).Max());
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = new double[_columnOf.DefaultIfEmpty(0).Max() + 1];
        var children = InternalChildren.Cast<UIElement>().ToList();
        for (var index = 0; index < children.Count && index < _columnOf.Length; index++)
        {
            var column = _columnOf[index];
            var height = children[index].DesiredSize.Height;
            children[index].Arrange(new Rect(column * (_columnWidth + ColumnGap), y[column], _columnWidth, height));
            y[column] += height;
        }

        return finalSize;
    }

    /// <summary>
    /// Each child's column, in order: the next column starts once the one being filled holds its share
    /// of the total height (a child going where most of it falls).
    /// </summary>
    private static int[] Split(IReadOnlyList<double> heights, int count)
    {
        var share = heights.Sum() / count;
        var columns = new int[heights.Count];
        var (column, filled) = (0, 0.0);
        for (var index = 0; index < heights.Count; index++)
        {
            if (column < count - 1 && filled > 0 && filled + (heights[index] / 2) > share * (column + 1))
            {
                column++;
            }

            columns[index] = column;
            filled += heights[index];
        }

        return columns;
    }
}
