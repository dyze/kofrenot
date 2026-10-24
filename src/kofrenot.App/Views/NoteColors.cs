using System.Windows.Media;
using Kofrenot.Core.Graph;

namespace Kofrenot.UI.Views;

/// <summary>
/// How the colours of nodes are drawn: a mid shade, seen on the light and the dark theme alike,
/// for dots and swatches; the same shade, see-through, as a background that stays pale on the
/// light theme and dark on the dark one.
/// </summary>
public static class NoteColors
{
    private const byte TintAlpha = 0x40;

    private static readonly Dictionary<NodeColor, Color> Shades = new()
    {
        [NodeColor.Yellow] = Color.FromRgb(0xF2, 0xC1, 0x3A),
        [NodeColor.Green] = Color.FromRgb(0x6F, 0xBF, 0x4A),
        [NodeColor.Blue] = Color.FromRgb(0x4A, 0x90, 0xE2),
        [NodeColor.Pink] = Color.FromRgb(0xE2, 0x6A, 0x9A),
        [NodeColor.Purple] = Color.FromRgb(0x8E, 0x7C, 0xE0),
        [NodeColor.Teal] = Color.FromRgb(0x2B, 0xB5, 0x9A),
        [NodeColor.Grey] = Color.FromRgb(0x9A, 0x9A, 0x9A),
    };

    /// <summary>The colours offered, none first.</summary>
    public static IReadOnlyList<NodeColor> Palette { get; } = Enum.GetValues<NodeColor>();

    /// <summary>A dot's or a swatch's brush; null for none.</summary>
    public static Brush? Fill(NodeColor color) => Shades.TryGetValue(color, out var shade) ? Frozen(shade) : null;

    /// <summary>A background's brush; null for none.</summary>
    public static Brush? Tint(NodeColor color) =>
        Shades.TryGetValue(color, out var shade) ? Frozen(Color.FromArgb(TintAlpha, shade.R, shade.G, shade.B)) : null;

    /// <summary>The colour's name, for a tooltip ("Yellow", "No colour").</summary>
    public static string NameOf(NodeColor color) => color == NodeColor.None ? "No colour" : color.ToString();

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
