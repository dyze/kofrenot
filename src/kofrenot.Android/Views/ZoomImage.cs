using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering;

namespace Kofrenot.Phone.Views;

/// <summary>
/// An image fitted to the screen, then zoomed by pinching (two fingers) or a double tap, and moved by
/// one finger; never smaller than fitted, never moved past its edges.
/// </summary>
public sealed class ZoomImage : Control, ICustomHitTest
{
    public static readonly StyledProperty<Bitmap?> SourceProperty = AvaloniaProperty.Register<ZoomImage, Bitmap?>(nameof(Source));

    private const double MaxScale = 8;
    private const double DoubleTapScale = 2.5;

    // The fingers down, where they are now.
    private readonly Dictionary<int, Point> _fingers = [];

    // How much the fitted image is zoomed, and where its top left corner is on the control.
    private double _scale = 1;
    private Point _origin;

    static ZoomImage()
    {
        AffectsRender<ZoomImage>(SourceProperty);
        ClipToBoundsProperty.OverrideDefaultValue<ZoomImage>(true);
    }

    public ZoomImage()
    {
        DoubleTapped += (_, e) => ZoomAt(e.GetPosition(this), _scale > 1 ? 1 / _scale : DoubleTapScale);
    }

    public Bitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Every point of the control takes the fingers, not only the image.</summary>
    public bool HitTest(Point point) => true;

    public override void Render(DrawingContext context)
    {
        if (Source is { } source)
        {
            context.DrawImage(source, new Rect(_origin, Fitted.Size * _scale));
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty || change.Property == BoundsProperty)
        {
            Fit();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _fingers[e.Pointer.Id] = e.GetPosition(this);
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_fingers.TryGetValue(e.Pointer.Id, out var before))
        {
            return;
        }

        var now = e.GetPosition(this);
        if (_fingers.Count == 1)
        {
            _origin += now - before;
        }
        else
        {
            // Two fingers: zoomed around the point between them, as far as they moved apart, and moved with it.
            var still = _fingers.First(finger => finger.Key != e.Pointer.Id).Value;
            var distanceBefore = Point.Distance(before, still);
            if (distanceBefore > 0)
            {
                var middleNow = Middle(now, still);
                _origin += middleNow - Middle(before, still);
                ZoomAt(middleNow, Point.Distance(now, still) / distanceBefore, render: false);
            }
        }

        _fingers[e.Pointer.Id] = now;
        Keep();
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _fingers.Remove(e.Pointer.Id);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _fingers.Remove(e.Pointer.Id);
    }

    // The image as large as fits, centred.
    private Rect Fitted
    {
        get
        {
            if (Source is not { Size: var size } || size.Width <= 0 || size.Height <= 0)
            {
                return default;
            }

            var ratio = Math.Min(Bounds.Width / size.Width, Bounds.Height / size.Height);
            var fitted = size * ratio;
            return new Rect(new Point((Bounds.Width - fitted.Width) / 2, (Bounds.Height - fitted.Height) / 2), fitted);
        }
    }

    private void Fit()
    {
        _scale = 1;
        _origin = Fitted.Position;
        InvalidateVisual();
    }

    // The point under the fingers stays where it is.
    private void ZoomAt(Point point, double factor, bool render = true)
    {
        var scale = Math.Clamp(_scale * factor, 1, MaxScale);
        factor = scale / _scale;
        _scale = scale;
        _origin = point - ((point - _origin) * factor);
        if (render)
        {
            Keep();
            InvalidateVisual();
        }
    }

    // Centred on a side smaller than the control; else its edges never inside the control.
    private void Keep()
    {
        var size = Fitted.Size * _scale;
        _origin = new Point(Within(_origin.X, size.Width, Bounds.Width), Within(_origin.Y, size.Height, Bounds.Height));
    }

    private static double Within(double start, double length, double room) =>
        length <= room ? (room - length) / 2 : Math.Clamp(start, room - length, 0);

    private static Point Middle(Point a, Point b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);
}
