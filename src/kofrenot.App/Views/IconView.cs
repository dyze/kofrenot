using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Kofrenot.Core.Browse;

namespace Kofrenot.UI.Views;

/// <summary>
/// One of kofrenot's icons (<see cref="IconPaths"/>), by name, <see cref="Size"/> pixels square, in the
/// foreground of where it is (a button's, hovered or disabled, the accent…), as a glyph of text would be.
/// </summary>
public sealed class IconView : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(string), typeof(IconView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(double), typeof(IconView), new FrameworkPropertyMetadata(14.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(IconView), new FrameworkPropertyMetadata(SystemColors.ControlTextBrush, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Dictionary<string, Geometry> Geometries = [];

    /// <summary>The icon's name; none drawn when null (a node type without icon), the room kept.</summary>
    public string? Kind
    {
        get => (string?)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>The icon's geometry on its 24×24 grid, parsed once.</summary>
    public static Geometry GeometryOf(string kind)
    {
        if (!Geometries.TryGetValue(kind, out var geometry))
        {
            geometry = Geometry.Parse(IconPaths.PathOf(kind));
            geometry.Freeze();
            Geometries[kind] = geometry;
        }

        return geometry;
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (string.IsNullOrEmpty(Kind))
        {
            return;
        }

        var scale = Size / 24;
        drawingContext.PushTransform(new ScaleTransform(scale, scale));
        drawingContext.DrawGeometry(Foreground, null, GeometryOf(Kind));
        drawingContext.Pop();
    }
}
