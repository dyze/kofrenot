namespace Kofrenot.Core.Datasets;

/// <summary>Makes the thumbnail of an image: decoding images needs Windows, so the App implements it.</summary>
public interface IThumbnailMaker
{
    /// <summary>
    /// The image (a JPEG, or a PNG when it has transparency), of <see cref="ThumbnailSize.Of"/> its
    /// size; null when it cannot be decoded.
    /// </summary>
    byte[]? Make(Stream image);
}

/// <summary>
/// How large a thumbnail is: sharp on a card, which fills its frame, so its shorter side reaches
/// <see cref="ShorterSide"/>, its longer side at most <see cref="LongerSide"/> (a panorama), never
/// larger than the image.
/// </summary>
public static class ThumbnailSize
{
    public const int ShorterSide = 256;

    public const int LongerSide = 512;

    /// <summary>The thumbnail's width and height for an image of <paramref name="width"/> by <paramref name="height"/> pixels, its proportions kept.</summary>
    public static (int Width, int Height) Of(int width, int height)
    {
        var scale = Math.Min(1.0, Math.Min((double)ShorterSide / Math.Min(width, height), (double)LongerSide / Math.Max(width, height)));
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }
}
