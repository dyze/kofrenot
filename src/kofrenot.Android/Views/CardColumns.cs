using Avalonia;
using Avalonia.Controls;

namespace Kofrenot.Phone.Views;

/// <summary>
/// Cards in columns, as Windows' MasonryPanel: as many columns as fit cards of <see cref="MinCardWidth"/>
/// (two on most phones), each card as tall as its content, put in the shortest column, in their order.
/// </summary>
public sealed class CardColumns : Panel
{
    public const double MinCardWidth = 150;
    private const double Gap = 8;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 2 * MinCardWidth + Gap : availableSize.Width;
        var cardWidth = CardWidth(width);
        foreach (var card in Children)
        {
            card.Measure(new Size(cardWidth, double.PositiveInfinity));
        }

        return new Size(width, Place(width).Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var (card, place) in Place(finalSize.Width).Places)
        {
            card.Arrange(place);
        }

        return finalSize;
    }

    private static int ColumnsFor(double width) => Math.Max(1, (int)((width + Gap) / (MinCardWidth + Gap)));

    private static double CardWidth(double width)
    {
        var columns = ColumnsFor(width);
        return Math.Max(0, (width - (Gap * (columns - 1))) / columns);
    }

    private (List<(Control Card, Rect Place)> Places, double Height) Place(double width)
    {
        var cardWidth = CardWidth(width);
        var bottoms = new double[ColumnsFor(width)];
        var places = new List<(Control, Rect)>();
        foreach (var card in Children)
        {
            var column = Array.IndexOf(bottoms, bottoms.Min());
            places.Add((card, new Rect(column * (cardWidth + Gap), bottoms[column], cardWidth, card.DesiredSize.Height)));
            bottoms[column] += card.DesiredSize.Height + Gap;
        }

        return (places, Math.Max(0, bottoms.Max() - Gap));
    }
}
