using Android.Graphics;
using Kofrenot.Core.Datasets;

namespace Kofrenot.Phone.Platform;

/// <summary>
/// Thumbnails by Android: the image decoded at a fraction of its size (<c>InSampleSize</c>), so a
/// large photo is never decoded whole, scaled to <see cref="ThumbnailSize.Of"/> its size, saved as
/// JPEG, or as PNG when it has transparency.
/// </summary>
public sealed class AndroidThumbnailMaker : IThumbnailMaker
{
    private const int JpegQuality = 85;

    public byte[]? Make(Stream image)
    {
        var bytes = new MemoryStream();
        image.CopyTo(bytes);
        var data = bytes.ToArray();

        // The size from the image's header, without decoding its pixels.
        var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
        BitmapFactory.DecodeByteArray(data, 0, data.Length, bounds);
        if (bounds.OutWidth <= 0 || bounds.OutHeight <= 0)
        {
            return null;
        }

        var (width, height) = ThumbnailSize.Of(bounds.OutWidth, bounds.OutHeight);
        var sample = 1;
        while (bounds.OutWidth / (sample * 2) >= width && bounds.OutHeight / (sample * 2) >= height)
        {
            sample *= 2;
        }

        using var decoded = BitmapFactory.DecodeByteArray(data, 0, data.Length, new BitmapFactory.Options { InSampleSize = sample });
        if (decoded is null)
        {
            return null;
        }

        using var thumbnail = Bitmap.CreateScaledBitmap(decoded, width, height, filter: true);
        var encoded = new MemoryStream();
        var format = thumbnail.HasAlpha ? Bitmap.CompressFormat.Png! : Bitmap.CompressFormat.Jpeg!;
        return thumbnail.Compress(format, JpegQuality, encoded) ? encoded.ToArray() : null;
    }
}
