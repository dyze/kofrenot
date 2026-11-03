using System.Windows.Media;

namespace Kofrenot.UI.Views;

/// <summary>
/// The colours of a graph view's link types (<see cref="Core.Views.GraphLegendItem.Color"/>): mid shades,
/// seen on the light and the dark theme alike, as <see cref="NoteColors"/>; past the last, they come round again.
/// </summary>
internal static class GraphPalette
{
    private static readonly Brush[] Brushes =
    [
        Frozen(0x4A, 0x90, 0xE2),
        Frozen(0xE8, 0x8A, 0x3C),
        Frozen(0x6F, 0xBF, 0x4A),
        Frozen(0xE2, 0x6A, 0x9A),
        Frozen(0x8E, 0x7C, 0xE0),
        Frozen(0x2B, 0xB5, 0x9A),
        Frozen(0xD9, 0xB2, 0x2B),
        Frozen(0x9A, 0x9A, 0x9A),
    ];

    public static Brush Of(int color) => Brushes[color % Brushes.Length];

    private static SolidColorBrush Frozen(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
