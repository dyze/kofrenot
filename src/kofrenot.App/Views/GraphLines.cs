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
/// </summary>
internal sealed class GraphLines(Canvas canvas)
{
    private readonly List<(GraphEdge Edge, Path Line)> _lines = [];

    public void Draw(IReadOnlyList<GraphEdge> edges, IReadOnlyDictionary<Guid, NodePosition> positions)
    {
        canvas.Children.Clear();
        _lines.Clear();
        foreach (var edge in edges)
        {
            var line = new Path { StrokeThickness = edge.Role == GraphRole.Beside ? 3 : 1.5, IsHitTestVisible = false };
            line.SetResourceReference(Shape.StrokeProperty, "ControlStrongStrokeColorDefaultBrush");
            if (edge.Role == GraphRole.Free)
            {
                line.StrokeDashArray = [4, 3];
            }

            line.Data = GeometryOf(edge, positions);
            canvas.Children.Add(line);
            _lines.Add((edge, line));
        }
    }

    /// <summary>Draws again the lines of a node being moved.</summary>
    public void Move(Guid node, IReadOnlyDictionary<Guid, NodePosition> positions)
    {
        foreach (var (edge, line) in _lines.Where(line => line.Edge.Link.From == node || line.Edge.Link.To == node))
        {
            line.Data = GeometryOf(edge, positions);
        }
    }

    private static Geometry GeometryOf(GraphEdge edge, IReadOnlyDictionary<Guid, NodePosition> positions)
    {
        var (from, to) = (positions[edge.Link.From], positions[edge.Link.To]);
        var (upper, lower) = edge.Role == GraphRole.Up ? (to, from) : (from, to);
        var (bottom, top) = (upper.Y + (GraphLayout.CardHeight / 2), lower.Y - (GraphLayout.CardHeight / 2));
        if (edge.Role is not (GraphRole.Down or GraphRole.Up) || bottom >= top)
        {
            return new LineGeometry(new Point(from.X, from.Y), new Point(to.X, to.Y));
        }

        var bend = (bottom + top) / 2;
        var figure = new PathFigure { StartPoint = new Point(upper.X, bottom) };
        figure.Segments.Add(new PolyLineSegment([new Point(upper.X, bend), new Point(lower.X, bend), new Point(lower.X, top)], isStroked: true));
        return new PathGeometry([figure]);
    }
}
