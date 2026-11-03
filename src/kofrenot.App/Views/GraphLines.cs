using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Path = System.Windows.Shapes.Path;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>
/// The lines of a graph view, by role: from the bottom of the upper card to the top of the lower one
/// with a bend between (<see cref="GraphRole.Down"/>, <see cref="GraphRole.Up"/>), a thick line between
/// cards side by side (<see cref="GraphRole.Beside"/>), a dashed one otherwise (<see cref="GraphRole.Free"/>).
/// Each in its link type's colour (<see cref="GraphPalette"/>), with an arrow on the card it leads to when
/// no rank says its direction; its name in a tooltip, and written on it when asked. A wide clear line over
/// each takes the mouse: its <see cref="FrameworkElement.Tag"/> is its <see cref="GraphEdge"/>.
/// </summary>
internal sealed class GraphLines(Canvas canvas)
{
    private const double HitThickness = 10;
    private const double HoverThickness = 1.5;
    private const double ArrowLength = 10;
    private const double ArrowHalfWidth = 5;
    private const double NameFontSize = 11;

    private readonly List<DrawnLine> _lines = [];

    /// <param name="nameOf">The name of a node shown, for the tooltips.</param>
    public void Draw(IReadOnlyList<GraphEdge> edges, IReadOnlyDictionary<Guid, NodePosition> positions, Func<Guid, string> nameOf, bool showsNames)
    {
        canvas.Children.Clear();
        _lines.Clear();
        foreach (var edge in edges)
        {
            var brush = GraphPalette.Of(edge.Color);
            var thickness = edge.Role == GraphRole.Beside ? 3 : 1.5;
            var line = new Path { Stroke = brush, StrokeThickness = thickness, IsHitTestVisible = false };
            if (edge.Role == GraphRole.Free)
            {
                line.StrokeDashArray = [4, 3];
            }

            var arrow = HasArrow(edge) ? new Path { Fill = brush, IsHitTestVisible = false } : null;
            var hit = new Path { Stroke = Brushes.Transparent, StrokeThickness = HitThickness, Tag = edge, ToolTip = $"{nameOf(edge.Link.From)} {edge.Name} {nameOf(edge.Link.To)}" };
            hit.MouseEnter += (_, _) => line.StrokeThickness = thickness + HoverThickness;
            hit.MouseLeave += (_, _) => line.StrokeThickness = thickness;
            var name = showsNames ? NameOf(edge) : null;
            var drawn = new DrawnLine(edge, line, arrow, hit, name);
            foreach (var element in new UIElement?[] { line, arrow, name, hit }.OfType<UIElement>())
            {
                canvas.Children.Add(element);
            }

            _lines.Add(drawn);
            Shape(drawn, positions);
        }
    }

    /// <summary>Draws again the lines of a node being moved.</summary>
    public void Move(Guid node, IReadOnlyDictionary<Guid, NodePosition> positions)
    {
        foreach (var drawn in _lines.Where(line => line.Edge.Link.From == node || line.Edge.Link.To == node))
        {
            Shape(drawn, positions);
        }
    }

    // A rank says which way a link goes: an arrow only for the others, when they have a way.
    private static bool HasArrow(GraphEdge edge) => edge.IsDirected && edge.Role is GraphRole.Beside or GraphRole.Free;

    private static TextBlock NameOf(GraphEdge edge)
    {
        var name = new TextBlock { Text = edge.Name, FontSize = NameFontSize, Padding = new Thickness(3, 0, 3, 0), IsHitTestVisible = false };
        name.SetResourceReference(TextBlock.BackgroundProperty, "SolidBackgroundFillColorBaseBrush");
        name.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return name;
    }

    private static void Shape(DrawnLine drawn, IReadOnlyDictionary<Guid, NodePosition> positions)
    {
        var (geometry, middle) = GeometryOf(drawn.Edge, positions);
        drawn.Line.Data = geometry;
        drawn.Hit.Data = geometry;
        if (drawn.Arrow is { } arrow)
        {
            arrow.Data = ArrowOf(positions[drawn.Edge.Link.From], positions[drawn.Edge.Link.To]);
        }

        if (drawn.Name is { } name)
        {
            Canvas.SetLeft(name, middle.X - (name.DesiredSize.Width / 2));
            Canvas.SetTop(name, middle.Y - (name.DesiredSize.Height / 2));
        }
    }

    // The line, and the middle of it, where its name goes.
    private static (Geometry Geometry, Point Middle) GeometryOf(GraphEdge edge, IReadOnlyDictionary<Guid, NodePosition> positions)
    {
        var (from, to) = (positions[edge.Link.From], positions[edge.Link.To]);
        var (upper, lower) = edge.Role == GraphRole.Up ? (to, from) : (from, to);
        var (bottom, top) = (upper.Y + (GraphLayout.CardHeight / 2), lower.Y - (GraphLayout.CardHeight / 2));
        if (edge.Role is not (GraphRole.Down or GraphRole.Up) || bottom >= top)
        {
            return BetweenBorders(from, to);
        }

        var bend = (bottom + top) / 2;
        var figure = new PathFigure { StartPoint = new Point(upper.X, bottom) };
        figure.Segments.Add(new PolyLineSegment([new Point(upper.X, bend), new Point(lower.X, bend), new Point(lower.X, top)], isStroked: true));
        return (new PathGeometry([figure]), new Point((upper.X + lower.X) / 2, bend));
    }

    // A straight line from border to border, so it never crosses a card; none when the cards cover one another.
    private static (Geometry Geometry, Point Middle) BetweenBorders(NodePosition from, NodePosition to)
    {
        var start = GraphLayout.BorderPoint(from, to.X, to.Y);
        var end = GraphLayout.BorderPoint(to, from.X, from.Y);
        var middle = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
        var coversOther = start == (from.X, from.Y) || end == (to.X, to.Y) || GraphLayout.CardAt([to], start.X, start.Y) is not null;
        return (coversOther ? Geometry.Empty : new LineGeometry(new Point(start.X, start.Y), new Point(end.X, end.Y)), middle);
    }

    // A triangle whose tip touches the card the link leads to.
    private static Geometry ArrowOf(NodePosition from, NodePosition to)
    {
        var (tipX, tipY) = GraphLayout.BorderPoint(to, from.X, from.Y);
        var direction = new Vector(tipX - from.X, tipY - from.Y);
        if (direction.Length == 0)
        {
            return Geometry.Empty;
        }

        direction.Normalize();
        var tip = new Point(tipX, tipY);
        var back = tip - (direction * ArrowLength);
        var side = new Vector(-direction.Y, direction.X) * ArrowHalfWidth;
        var figure = new PathFigure { StartPoint = tip, IsClosed = true, IsFilled = true };
        figure.Segments.Add(new PolyLineSegment([back + side, back - side], isStroked: false));
        return new PathGeometry([figure]);
    }

    private sealed record DrawnLine(GraphEdge Edge, Path Line, Path? Arrow, Path Hit, TextBlock? Name);
}
