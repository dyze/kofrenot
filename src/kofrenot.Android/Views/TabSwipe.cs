using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Kofrenot.Core.Logging;

namespace Kofrenot.Phone.Views;

/// <summary>
/// A finger swiped across a page's content, which follows it: the next tab (to the left) or the previous one (to the right),
/// once past a third of the width or flicked, the content fading out then the new one in; otherwise it slides back.
/// Its direction is decided over its first few pixels: sideways, it is a swipe, the content taking the finger
/// from a list that began to scroll; otherwise the list scrolls. With no tab on that side, the content follows at a third and springs back.
/// A table scrolled sideways swipes once at its edge; none from the screen's edges, where Android's back gesture starts.
/// </summary>
public sealed class TabSwipe
{
    // As Android's touch slop: the move that decides between a swipe and a scroll.
    private const double DecidingDistance = 16;
    private const double SwitchingShare = 1.0 / 3;
    private const double RubberBand = 3;
    private const double MaxFollowShare = 0.75;
    private const double FlickSpeed = 0.4; // dp per ms
    private const ulong FlickWindowMs = 100;
    private const double SystemEdge = 20;
    private const double Tolerance = 0.5;
    // As Material's shared axis: the list slides on a little while fading out, the next one comes in from as far.
    private const double FadingShift = 32;
    private static readonly TimeSpan FadeOutDuration = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan SlideDuration = TimeSpan.FromMilliseconds(150);

    private enum Phase { None, Deciding, Swiping, Sliding }

    private readonly ScrollViewer _content;
    private readonly Func<int, bool> _hasTab;
    private readonly Action<int> _swiped;
    private readonly ILog _log;
    private readonly TranslateTransform _shift = new();
    private readonly Queue<(ulong Time, double X)> _recent = new();
    private Phase _phase;
    private Point _start;
    private bool _atLeftEnd;
    private bool _atRightEnd;
    private bool _hasNext;
    private bool _hasPrevious;

    private TabSwipe(ScrollViewer content, Func<int, bool> hasTab, Action<int> swiped, ILog log)
    {
        _content = content;
        _hasTab = hasTab;
        _swiped = swiped;
        _log = log;
        content.RenderTransform = _shift;

        // Tunnelled, and when handled too: seen before the list inside, and while it scrolls.
        content.AddHandler(InputElement.PointerPressedEvent, Pressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        content.AddHandler(InputElement.PointerMovedEvent, Moved, RoutingStrategies.Tunnel, handledEventsToo: true);
        content.AddHandler(InputElement.PointerReleasedEvent, Released, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>
    /// <paramref name="hasTab"/> tells whether there is a tab on a side, <paramref name="swiped"/> goes to it:
    /// 1 for the next tab, -1 for the previous one.
    /// </summary>
    public static void Watch(ScrollViewer content, Func<int, bool> hasTab, Action<int> swiped, ILog log) =>
        _ = new TabSwipe(content, hasTab, swiped, log);

    private double Width => _content.Bounds.Width;

    // Where the finger is on the screen: not on the content, which moves under it.
    private static Point PositionOf(PointerEventArgs e) => e.GetPosition(null);

    private void Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (_phase == Phase.Sliding)
        {
            return;
        }

        // A gesture Android took back (no release seen) leaves nothing shifted.
        _shift.X = 0;
        _content.Opacity = 1;
        _start = PositionOf(e);
        var inContent = e.GetPosition(_content).X;
        var fromSystemEdge = inContent < SystemEdge || inContent > Width - SystemEdge;
        _phase = fromSystemEdge ? Phase.None : Phase.Deciding;
        _atLeftEnd = _content.Offset.X <= Tolerance;
        _atRightEnd = _content.Offset.X >= _content.Extent.Width - _content.Viewport.Width - Tolerance;
        _recent.Clear();
    }

    private void Moved(object? sender, PointerEventArgs e)
    {
        if (_phase is Phase.Deciding)
        {
            var move = PositionOf(e) - _start;
            if (Math.Abs(move.X) < DecidingDistance && Math.Abs(move.Y) < DecidingDistance)
            {
                return;
            }

            Decide(move);

            // The content takes the finger: a scroll the list began ends there, and no row gets the release as a tap.
            if (_phase is Phase.Swiping)
            {
                e.Pointer.Capture(_content);
            }
        }

        if (_phase is Phase.Swiping)
        {
            var x = PositionOf(e).X - _start.X;
            Remember(e.Timestamp, x);
            _shift.X = Follow(x);
        }
    }

    private void Decide(Point move)
    {
        var sideways = Math.Abs(move.X) > Math.Abs(move.Y);
        var atEnd = IsAtEnd(StepOf(move.X));
        _phase = sideways && atEnd ? Phase.Swiping : Phase.None;
        _hasNext = _hasTab(1);
        _hasPrevious = _hasTab(-1);
        if (_phase is Phase.None)
        {
            _log.Debug("Action", $"Not a swipe, scrolled: moved {move.X:0} x {move.Y:0} dp, {(sideways ? "table not at its end" : "up or down")}");
        }
    }

    private async void Released(object? sender, PointerReleasedEventArgs e)
    {
        if (_phase is not Phase.Swiping)
        {
            if (_phase is Phase.Deciding)
            {
                _phase = Phase.None;
            }

            return;
        }

        // Not a tap on the row the finger left from.
        e.Handled = true;
        var x = PositionOf(e).X - _start.X;
        Remember(e.Timestamp, x);
        var step = StepOf(x);
        var speed = Speed();
        var flicked = Math.Abs(speed) >= FlickSpeed && Math.Sign(speed) == Math.Sign(x);
        var far = Math.Abs(x) >= Width * SwitchingShare;
        var switches = IsAtEnd(step) && HasTab(step) && (far || flicked);
        _log.Debug("Action", $"Swipe: moved {x:0} dp of {Width:0} at {speed:0.00} dp/ms, {(switches ? "tab switched" : HasTab(step) ? "slid back" : "no tab on that side")}");

        // Never wholly out of sight: a list there shows a single row, a card no preview.
        _phase = Phase.Sliding;
        if (switches)
        {
            await Slide.ToAsync(_content, _shift, _shift.X - step * FadingShift, 0, FadeOutDuration);
            _shift.X = step * FadingShift;
            _swiped(step);
        }

        await Slide.ToAsync(_content, _shift, 0, 1, SlideDuration);
        _phase = Phase.None;
    }

    // Under the finger, a quarter always in sight; at a third with no tab on that side; still on a table's side not at its end.
    private double Follow(double x)
    {
        var step = StepOf(x);
        var reach = Width * MaxFollowShare;
        return !IsAtEnd(step) ? 0 : HasTab(step) ? Math.Clamp(x, -reach, reach) : x / RubberBand;
    }

    private void Remember(ulong time, double x)
    {
        _recent.Enqueue((time, x));
        while (_recent.Count > 2 && time - _recent.Peek().Time > FlickWindowMs)
        {
            _recent.Dequeue();
        }
    }

    // Over the last moves, in dp per ms; positive to the right.
    private double Speed()
    {
        if (_recent.Count < 2)
        {
            return 0;
        }

        var (firstTime, firstX) = _recent.Peek();
        var (lastTime, lastX) = _recent.Last();
        return lastTime > firstTime ? (lastX - firstX) / (lastTime - firstTime) : 0;
    }

    // Left: the next tab; right: the previous one.
    private static int StepOf(double x) => x < 0 ? 1 : -1;

    private bool HasTab(int step) => step > 0 ? _hasNext : _hasPrevious;

    // A table scrolled sideways swipes only from its end on that side.
    private bool IsAtEnd(int step) => step > 0 ? _atRightEnd : _atLeftEnd;
}
