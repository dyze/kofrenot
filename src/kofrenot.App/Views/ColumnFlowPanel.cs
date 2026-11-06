using System.Windows;
using System.Windows.Controls;

namespace Kofrenot.UI.Views;

/// <summary>
/// Rows top to bottom, in side-by-side columns of the same width: each column starts at the row
/// <see cref="StartsProperty"/> gives (<see cref="Core.Input.HomeColumns"/>), set on the items
/// control and inherited. One column by default.
/// </summary>
public sealed class ColumnFlowPanel : Panel
{
    private const double Gap = 16;

    public static readonly DependencyProperty StartsProperty = DependencyProperty.RegisterAttached(
        "Starts", typeof(IReadOnlyList<int>), typeof(ColumnFlowPanel),
        new FrameworkPropertyMetadata(new[] { 0 }, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static IReadOnlyList<int> GetStarts(DependencyObject element) => (IReadOnlyList<int>)element.GetValue(StartsProperty);

    public static void SetStarts(DependencyObject element, IReadOnlyList<int> value) => element.SetValue(StartsProperty, value);

    private IReadOnlyList<int> Starts => GetStarts(this);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = ColumnWidth(availableSize.Width);
        var heights = new double[Starts.Count];
        var widest = 0.0;
        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var row = InternalChildren[index];
            row.Measure(new Size(width, double.PositiveInfinity));
            heights[ColumnOf(index)] += row.DesiredSize.Height;
            widest = Math.Max(widest, row.DesiredSize.Width);
        }

        var shownWidth = double.IsInfinity(availableSize.Width) ? widest * Starts.Count + Gap * (Starts.Count - 1) : availableSize.Width;
        return new Size(shownWidth, heights.DefaultIfEmpty(0).Max());
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = ColumnWidth(finalSize.Width);
        var tops = new double[Starts.Count];
        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var row = InternalChildren[index];
            var column = ColumnOf(index);
            row.Arrange(new Rect(column * (width + Gap), tops[column], width, row.DesiredSize.Height));
            tops[column] += row.DesiredSize.Height;
        }

        return finalSize;
    }

    private double ColumnWidth(double available) =>
        double.IsInfinity(available) ? double.PositiveInfinity : Math.Max(0, (available - Gap * (Starts.Count - 1)) / Starts.Count);

    private int ColumnOf(int index) => Math.Max(0, Starts.Count(start => start <= index) - 1);
}
