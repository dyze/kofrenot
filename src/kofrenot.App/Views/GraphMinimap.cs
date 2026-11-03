using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>
/// A graph view's minimap, in a corner: every card as a small box, and the part shown as a frame.
/// A click or a drag on it centres the view there (<see cref="CentreOn"/>). Hidden while the whole
/// graph is shown.
/// </summary>
internal sealed class GraphMinimap : Border
{
    private const double MapWidth = 180;
    private const double MapHeight = 120;
    private const double Inset = 6;

    private readonly Canvas _map = new() { Width = MapWidth, Height = MapHeight, ClipToBounds = true };
    private readonly Rectangle _frame = new() { StrokeThickness = 1.5, IsHitTestVisible = false };
    private GraphBounds? _bounds;
    private double _scale = 1;

    public GraphMinimap()
    {
        Child = _map;
        CornerRadius = new CornerRadius(6);
        BorderThickness = new Thickness(1);
        Cursor = Cursors.Hand;
        ToolTip = "Click or drag to go there";
        SetResourceReference(BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        SetResourceReference(BorderBrushProperty, "ControlStrongStrokeColorDefaultBrush");
        _frame.SetResourceReference(Shape.StrokeProperty, "AccentFillColorDefaultBrush");
        MouseLeftButtonDown += (_, e) =>
        {
            CaptureMouse();
            GoTo(e);
        };
        MouseMove += (_, e) =>
        {
            if (IsMouseCaptured)
            {
                GoTo(e);
            }
        };
        MouseLeftButtonUp += (_, _) => ReleaseMouseCapture();
    }

    /// <summary>Asks to centre the view on a point of the graph, in the view's units.</summary>
    public event Action<Point>? CentreOn;

    /// <summary>Draws the cards again, scaled to fit the map.</summary>
    public void Draw(IReadOnlyCollection<NodePosition> cards)
    {
        _map.Children.Clear();
        _bounds = GraphLayout.BoundsOf(cards);
        if (_bounds is not { } bounds)
        {
            return;
        }

        _scale = Math.Min((MapWidth - (2 * Inset)) / bounds.Width, (MapHeight - (2 * Inset)) / bounds.Height);
        foreach (var card in cards)
        {
            var box = new Rectangle { Width = GraphLayout.CardWidth * _scale, Height = GraphLayout.CardHeight * _scale, IsHitTestVisible = false };
            box.SetResourceReference(Shape.FillProperty, "ControlStrongStrokeColorDefaultBrush");
            var (left, top) = ToMap(card.X - (GraphLayout.CardWidth / 2), card.Y - (GraphLayout.CardHeight / 2));
            Canvas.SetLeft(box, left);
            Canvas.SetTop(box, top);
            _map.Children.Add(box);
        }

        _map.Children.Add(_frame);
    }

    /// <summary>Frames the part of the graph shown; hidden when it shows every card.</summary>
    public void Frame(Rect shown)
    {
        var isWhole = _bounds is not { } bounds
            || (shown.Left <= bounds.Left && shown.Top <= bounds.Top && shown.Right >= bounds.Right && shown.Bottom >= bounds.Bottom);
        Visibility = isWhole ? Visibility.Collapsed : Visibility.Visible;
        if (isWhole)
        {
            return;
        }

        var (left, top) = ToMap(shown.Left, shown.Top);
        Canvas.SetLeft(_frame, left);
        Canvas.SetTop(_frame, top);
        _frame.Width = shown.Width * _scale;
        _frame.Height = shown.Height * _scale;
    }

    private (double Left, double Top) ToMap(double x, double y) => (Inset + ((x - _bounds!.Left) * _scale), Inset + ((y - _bounds.Top) * _scale));

    private void GoTo(MouseEventArgs e)
    {
        if (_bounds is { } bounds)
        {
            var at = e.GetPosition(_map);
            CentreOn?.Invoke(new Point(bounds.Left + ((at.X - Inset) / _scale), bounds.Top + ((at.Y - Inset) / _scale)));
        }

        e.Handled = true;
    }
}
