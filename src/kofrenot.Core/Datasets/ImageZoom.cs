using System.Globalization;

namespace Kofrenot.Core.Datasets;

/// <summary>
/// The zoom of the image window: 1 shows one image pixel per screen pixel. It starts fitted to the
/// window (never enlarged); each step in or out multiplies or divides it by <see cref="Step"/>,
/// within <see cref="Min"/> and <see cref="Max"/>.
/// </summary>
public static class ImageZoom
{
    public const double Min = 0.05;
    public const double Max = 16;
    public const double Step = 1.25;

    /// <summary>The zoom showing the whole image in the area, at most 1; 1 when either is empty.</summary>
    public static double Fit(double areaWidth, double areaHeight, int imageWidth, int imageHeight) =>
        areaWidth <= 0 || areaHeight <= 0 || imageWidth <= 0 || imageHeight <= 0
            ? 1
            : Math.Clamp(Math.Min(areaWidth / imageWidth, areaHeight / imageHeight), Min, 1);

    public static double In(double zoom) => Math.Min(Max, zoom * Step);

    public static double Out(double zoom) => Math.Max(Min, zoom / Step);

    /// <summary>"150 %".</summary>
    public static string Percent(double zoom, IFormatProvider? culture = null) =>
        string.Create(culture ?? CultureInfo.CurrentCulture, $"{Math.Round(zoom * 100)} %");
}
