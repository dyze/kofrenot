using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Kofrenot.Phone.Views;

/// <summary>
/// A finger swiped across a page's content: the next tab (to the left) or the previous one (to the right).
/// Its direction is decided over its first few pixels, the scrolls inside kept out meanwhile: sideways, it is a swipe,
/// and no list scrolls; otherwise it scrolls. A table scrolled sideways swipes once at its edge;
/// none from the screen's edges, where Android's back gesture starts.
/// </summary>
public sealed class TabSwipe
{
    // As Android's touch slop: the move that decides between a swipe and a scroll.
    private const double DecidingDistance = 16;
    private const double MinDistance = 48;
    private const double SystemEdge = 20;
    private const double Tolerance = 0.5;

    private enum Phase { None, Deciding, Swiping }

    private readonly ScrollViewer _content;
    private readonly Action<int> _swiped;
    private Phase _phase;
    private Point _start;
    private bool _atLeftEnd;
    private bool _atRightEnd;

    private TabSwipe(ScrollViewer content, Action<int> swiped)
    {
        _content = content;
        _swiped = swiped;

        // Tunnelled, and when handled too: seen before the list inside, whose scroll then waits for the decision.
        content.AddHandler(InputElement.PointerPressedEvent, Pressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        content.AddHandler(InputElement.PointerMovedEvent, Moved, RoutingStrategies.Tunnel, handledEventsToo: true);
        content.AddHandler(InputElement.PointerReleasedEvent, Released, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary><paramref name="swiped"/> gets 1 for the next tab, -1 for the previous one.</summary>
    public static void Watch(ScrollViewer content, Action<int> swiped) => _ = new TabSwipe(content, swiped);

    private void Pressed(object? sender, PointerPressedEventArgs e)
    {
        _start = e.GetPosition(_content);
        var fromSystemEdge = _start.X < SystemEdge || _start.X > _content.Bounds.Width - SystemEdge;
        _phase = fromSystemEdge ? Phase.None : Phase.Deciding;
        _atLeftEnd = _content.Offset.X <= Tolerance;
        _atRightEnd = _content.Offset.X >= _content.Extent.Width - _content.Viewport.Width - Tolerance;
    }

    private void Moved(object? sender, PointerEventArgs e)
    {
        if (_phase == Phase.Deciding)
        {
            var move = e.GetPosition(_content) - _start;
            if (Math.Abs(move.X) < DecidingDistance && Math.Abs(move.Y) < DecidingDistance)
            {
                e.PreventGestureRecognition();
                return;
            }

            _phase = Math.Abs(move.X) > Math.Abs(move.Y) && IsAtEnd(StepOf(move)) ? Phase.Swiping : Phase.None;
        }

        if (_phase == Phase.Swiping)
        {
            e.PreventGestureRecognition();
        }
    }

    private void Released(object? sender, PointerReleasedEventArgs e)
    {
        if (_phase != Phase.Swiping)
        {
            _phase = Phase.None;
            return;
        }

        _phase = Phase.None;
        e.PreventGestureRecognition();

        // Not a tap on the row the finger left from.
        e.Handled = true;
        var move = e.GetPosition(_content) - _start;
        if (Math.Abs(move.X) >= MinDistance && IsAtEnd(StepOf(move)))
        {
            _swiped(StepOf(move));
        }
    }

    // Left: the next tab; right: the previous one.
    private static int StepOf(Point move) => move.X < 0 ? 1 : -1;

    // A table scrolled sideways swipes only from its end on that side.
    private bool IsAtEnd(int step) => step > 0 ? _atRightEnd : _atLeftEnd;
}
