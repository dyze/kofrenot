using System.IO;
using System.Windows.Media.Imaging;
using Kofrenot.Core.Datasets;

namespace Kofrenot.UI.Platform;

/// <summary>
/// Thumbnails by WPF: the image decoded straight to <see cref="IThumbnailMaker.Size"/> on its
/// longest side (so a large photo is never decoded whole), saved as PNG. Thread-safe: it uses frozen bitmaps only.
/// </summary>
internal sealed class WpfThumbnailMaker : IThumbnailMaker
{
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
            if (width >= height && width > IThumbnailMaker.Size)
            {
                thumbnail.DecodePixelWidth = IThumbnailMaker.Size;
            }
            else if (height > width && height > IThumbnailMaker.Size)
            {
                thumbnail.DecodePixelHeight = IThumbnailMaker.Size;
            }

            thumbnail.EndInit();
            thumbnail.Freeze();

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(thumbnail));
            var png = new MemoryStream();
            encoder.Save(png);
            return png.ToArray();
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
}
