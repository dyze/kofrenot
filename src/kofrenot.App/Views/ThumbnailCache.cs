using System.IO;
using System.Windows.Media.Imaging;
using Kofrenot.Core.Storage;

namespace Kofrenot.UI.Views;

/// <summary>
/// The thumbnails shown in the rows, decrypted and decoded off the UI thread, kept in memory only
/// (the latest <see cref="Capacity"/>), so typing in the input box does not read them again. A
/// thumbnail not synced yet is not remembered: it is looked for again by the next row.
/// </summary>
public sealed class ThumbnailCache
{
    private const int Capacity = 200;

    private readonly Dictionary<Guid, BitmapSource> _images = [];
    private readonly LinkedList<Guid> _order = [];
    private readonly Dictionary<Guid, Task<BitmapSource?>> _loading = [];
    private ThumbnailFiles? _files;

    /// <summary>The one cache of the app: rows are built by templates, so they find it here.</summary>
    public static ThumbnailCache Shared { get; } = new();

    /// <summary>The open vault's thumbnails; another vault (or none) empties the cache.</summary>
    public void Use(ThumbnailFiles? files)
    {
        if (files == _files)
        {
            return;
        }

        _files = files;
        _images.Clear();
        _order.Clear();
        _loading.Clear();
    }

    /// <summary>The thumbnail of a version, or null when there is none (yet). Call it on the UI thread.</summary>
    public async Task<BitmapSource?> Get(Guid version)
    {
        if (_images.TryGetValue(version, out var image))
        {
            return image;
        }

        if (_files is not { } files)
        {
            return null;
        }

        if (!_loading.TryGetValue(version, out var loading))
        {
            _loading[version] = loading = Task.Run(() => Decode(files.Read(version)));
        }

        image = await loading;
        _loading.Remove(version);
        if (image is not null && files == _files)
        {
            Remember(version, image);
        }

        return image;
    }

    private void Remember(Guid version, BitmapSource image)
    {
        if (_images.TryAdd(version, image))
        {
            _order.AddLast(version);
        }

        if (_order.Count > Capacity)
        {
            _images.Remove(_order.First!.Value);
            _order.RemoveFirst();
        }
    }

    private static BitmapSource? Decode(byte[]? png)
    {
        if (png is null)
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(png);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException or ArgumentException)
        {
            return null;
        }
    }
}
