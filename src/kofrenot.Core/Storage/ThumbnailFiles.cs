using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Storage;

/// <summary>
/// The thumbnails of dataset versions, one small file each (<c>content/</c>), written once and
/// never changed: a PNG sealed with the vault's key (<see cref="SealKind.Thumbnail"/>), bound to the version id (the file's name).
/// </summary>
public sealed class ThumbnailFiles(string vaultFolder, KeyedBox box, ILog? log = null)
{
    private readonly ILog _log = log ?? NullLog.Instance;

    /// <exception cref="IOException">The file cannot be written.</exception>
    public void Write(Guid version, byte[] png)
    {
        var path = VaultFolder.ThumbnailFileOf(vaultFolder, version);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.Write(path, box.Seal(SealKind.Thumbnail, png, version.ToByteArray()), overwrite: false);
    }

    /// <summary>
    /// The thumbnail of a version, or null when there is none, it is not synced yet, or it cannot
    /// be read (logged).
    /// </summary>
    public byte[]? Read(Guid version)
    {
        var path = VaultFolder.ThumbnailFileOf(vaultFolder, version);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return box.Open(SealKind.Thumbnail, File.ReadAllBytes(path), version.ToByteArray());
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            _log.Warn("Vault", $"Thumbnail {version} cannot be read: {ex.Message}");
            return null;
        }
    }
}
