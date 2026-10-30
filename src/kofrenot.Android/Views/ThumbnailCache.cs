using Avalonia.Media.Imaging;
using Kofrenot.Core.Storage;

namespace Kofrenot.Phone.Views;

/// <summary>
/// The thumbnails shown in the rows and on a node's page, as on Windows: decrypted and decoded off
/// the UI thread, kept in memory only (the latest <see cref="Capacity"/>), so typing in the search
/// box does not read them again. A thumbnail not here yet is not remembered: a server vault
/// downloads it (<see cref="Vault.ReadThumbnail"/>), and the page shown again once it is here looks
/// for it again.
/// </summary>
public sealed class ThumbnailCache
{
    private const int Capacity = 100;

    private readonly Dictionary<Guid, Bitmap> _images = [];
    private readonly LinkedList<Guid> _order = [];
    private readonly Dictionary<Guid, Task<Bitmap?>> _loading = [];
    private Vault? _vault;

    /// <summary>The one cache of the app: rows are built by templates, so they find it here.</summary>
    public static ThumbnailCache Shared { get; } = new();

    /// <summary>The open vault, whose thumbnails are shown; another vault (or none) empties the cache.</summary>
    public void Use(Vault? vault)
    {
        if (vault == _vault)
        {
            return;
        }

        _vault = vault;
        _images.Clear();
        _order.Clear();
        _loading.Clear();
    }

    /// <summary>The thumbnail of a version, or null when there is none (yet). Call it on the UI thread.</summary>
    public async Task<Bitmap?> Get(Guid version)
    {
        if (_images.TryGetValue(version, out var image))
        {
            return image;
        }

        if (_vault is not { } vault)
        {
            return null;
        }

        if (!_loading.TryGetValue(version, out var loading))
        {
            _loading[version] = loading = Task.Run(() => Decode(vault.ReadThumbnail(version)));
        }

        image = await loading;
        _loading.Remove(version);
        if (image is not null && vault == _vault)
        {
            Remember(version, image);
        }

        return image;
    }

    private void Remember(Guid version, Bitmap image)
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

    private static Bitmap? Decode(byte[]? png)
    {
        if (png is null)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(png);
            return new Bitmap(stream);
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
