using Avalonia;
using Avalonia.Controls;
using Kofrenot.Core.Input;

namespace Kofrenot.Phone.Views;

/// <summary>
/// The home's rows in columns, as on Windows: each column from one of <see cref="StartsProperty"/>
/// (<see cref="HomeColumns.Starts"/>, set on the list and inherited) to the next, top to bottom,
/// the columns sharing the width. One start: a plain stack. The rows of the last column lower than
/// <see cref="CornerTopProperty"/> are narrower by <see cref="CornerWidth"/>, clear of ＋ beside them.
/// </summary>
public sealed class HomeColumnsPanel : Panel
{
    /// <summary>The width ＋ takes at the bottom right, with its margins and a gap.</summary>
    public const double CornerWidth = 80;

    public static readonly AttachedProperty<IReadOnlyList<int>> StartsProperty =
        AvaloniaProperty.RegisterAttached<HomeColumnsPanel, Control, IReadOnlyList<int>>("Starts", [0], inherits: true);

    public static readonly AttachedProperty<double> CornerTopProperty =
        AvaloniaProperty.RegisterAttached<HomeColumnsPanel, Control, double>("CornerTop", double.PositiveInfinity, inherits: true);

    static HomeColumnsPanel() => AffectsMeasure<HomeColumnsPanel>(StartsProperty, CornerTopProperty);

    public static void SetStarts(Control control, IReadOnlyList<int> starts) => control.SetValue(StartsProperty, starts);

    /// <summary>Where ＋ starts, from the top of the list: the rows beside it, in the last column, made narrower.</summary>
    public static void SetCornerTop(Control control, double top) => control.SetValue(CornerTopProperty, top);

    protected override Size MeasureOverride(Size availableSize)
    {
        if (double.IsInfinity(availableSize.Width))
        {
            foreach (var row in Children)
            {
                row.Measure(availableSize);
            }

            return new Size(Children.Select(row => row.DesiredSize.Width).DefaultIfEmpty(0).Max(), Children.Sum(row => row.DesiredSize.Height));
        }

        return new Size(availableSize.Width, Place(availableSize.Width, measure: true).Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var (row, place) in Place(finalSize.Width, measure: false).Places)
        {
            row.Arrange(place);
        }

        return finalSize;
    }

    // Each row's place, column by column; measured first (narrower beside ＋) when asked.
    private (List<(Control Row, Rect Place)> Places, double Height) Place(double width, bool measure)
    {
        var columns = Columns();
        var columnWidth = width / columns.Count;
        var cornerTop = GetValue(CornerTopProperty);
        var places = new List<(Control, Rect)>();
        var tallest = 0.0;
        for (var column = 0; column < columns.Count; column++)
        {
            var y = 0.0;
            for (var index = columns[column].First; index < columns[column].End; index++)
            {
                var row = Children[index];
                var rowWidth = columnWidth;
                if (measure)
                {
                    row.Measure(new Size(rowWidth, double.PositiveInfinity));
                }

                if (column == columns.Count - 1 && y + row.DesiredSize.Height > cornerTop)
                {
                    rowWidth = Math.Max(0, columnWidth - CornerWidth);
                    if (measure)
                    {
                        row.Measure(new Size(rowWidth, double.PositiveInfinity));
                    }
                }

                places.Add((row, new Rect(column * columnWidth, y, rowWidth, row.DesiredSize.Height)));
                y += row.DesiredSize.Height;
            }

            tallest = Math.Max(tallest, y);
        }

        return (places, tallest);
    }

    // Each column's rows, [First, End): the starts within the rows there are, the first one at 0.
    private List<(int First, int End)> Columns()
    {
        var starts = GetValue(StartsProperty).Where(start => start > 0 && start < Children.Count).Distinct().Order().Prepend(0).ToList();
        return [.. starts.Select((first, column) => (first, column + 1 < starts.Count ? starts[column + 1] : Children.Count))];
    }
}
