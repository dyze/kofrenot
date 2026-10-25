using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kofrenot.Core.Datasets;

namespace Kofrenot.UI.Platform;

/// <summary>
/// Thumbnails by WPF: the image decoded straight to <see cref="ThumbnailSize.Of"/> its size (so a
/// large photo is never decoded whole), saved as JPEG, or as PNG when it has transparency.
/// Thread-safe: it uses frozen bitmaps only.
/// </summary>
internal sealed class WpfThumbnailMaker : IThumbnailMaker
{
    private const int JpegQuality = 85;

    public byte[]? Make(Stream image)
    {
        try
        {
            var bytes = new MemoryStream();
            image.CopyTo(bytes);
            var (width, height) = SizeOf(bytes);
            bytes.Position = 0;

            var thumbnail = new BitmapImage();
            thumbnail.BeginInit();
            thumbnail.CacheOption = BitmapCacheOption.OnLoad;
            thumbnail.StreamSource = bytes;

            // One side only, so the other keeps the image's proportions; a small image is not enlarged.
            var (thumbnailWidth, _) = ThumbnailSize.Of(width, height);
            if (thumbnailWidth < width)
            {
                thumbnail.DecodePixelWidth = thumbnailWidth;
            }

            thumbnail.EndInit();
            thumbnail.Freeze();

            BitmapEncoder encoder = HasTransparency(thumbnail) ? new PngBitmapEncoder() : new JpegBitmapEncoder { QualityLevel = JpegQuality };
            encoder.Frames.Add(BitmapFrame.Create(thumbnail));
            var encoded = new MemoryStream();
            encoder.Save(encoded);
            return encoded.ToArray();
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    // Reads the size from the image's header, without decoding its pixels.
    private static (int Width, int Height) SizeOf(MemoryStream bytes)
    {
        bytes.Position = 0;
        var frame = BitmapDecoder.Create(bytes, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
        return (frame.PixelWidth, frame.PixelHeight);
    }

    // An alpha channel, or a palette with a see-through colour (a GIF, a PNG of 256 colours): JPEG would lose it.
    private static bool HasTransparency(BitmapSource image) =>
        image.Format == PixelFormats.Bgra32 || image.Format == PixelFormats.Pbgra32
        || image.Format == PixelFormats.Rgba64 || image.Format == PixelFormats.Prgba64
        || image.Format == PixelFormats.Rgba128Float || image.Format == PixelFormats.Prgba128Float
        || image.Palette?.Colors.Any(color => color.A < byte.MaxValue) == true;
}
