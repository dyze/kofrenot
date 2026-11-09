using Android.Graphics;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace Kofrenot.Phone.Platform;

/// <summary>
/// An image read to be shown whole on the screen: a large photo decoded at most <see cref="MaxSide"/> pixels
/// on its longer side (enough to zoom in), so it never takes the phone's memory; a smaller one as it is.
/// </summary>
public static class ScreenImage
{
    public const int MaxSide = 4096;

    /// <summary>The image; null when it cannot be read as one.</summary>
    public static AvaloniaBitmap? Of(byte[] data)
    {
        // The size from the image's header, without decoding its pixels.
        var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
        BitmapFactory.DecodeByteArray(data, 0, data.Length, bounds);
        if (bounds.OutWidth <= 0 || bounds.OutHeight <= 0)
        {
            return null;
        }

        using var stream = new MemoryStream(data);
        return bounds.OutWidth >= bounds.OutHeight
            ? bounds.OutWidth > MaxSide ? AvaloniaBitmap.DecodeToWidth(stream, MaxSide) : new AvaloniaBitmap(stream)
            : bounds.OutHeight > MaxSide ? AvaloniaBitmap.DecodeToHeight(stream, MaxSide) : new AvaloniaBitmap(stream);
    }
}
