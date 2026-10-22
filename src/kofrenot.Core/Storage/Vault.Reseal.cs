using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Storage;

/// <summary>
/// Files sealed again with the latest key: written here under an older epoch, then refused by the
/// server's fence after a rotation. They were never uploaded, so no other device read them: the
/// same content, under the same id, sealed again.
/// </summary>
public sealed partial class Vault
{
    /// <summary>Seals again the files at <paramref name="paths"/> (store paths: <c>changes/3f/3f9c….kfc</c>) not sealed with the latest key. Returns how many.</summary>
    /// <exception cref="IOException">A file cannot be read or written.</exception>
    /// <exception cref="InvalidDataException">A file is damaged, or of an epoch whose key is missing.</exception>
    public int Reseal(IEnumerable<string> paths)
    {
        var resealed = 0;
        foreach (var path in paths)
        {
            var local = Path.Combine([_folder, .. path.Split('/')]);
            if (KindOf(local) is not { } kind
                || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(local), "N", out var id)
                || !File.Exists(local)
                || EpochOf(local) == _box.Keys.CurrentEpoch)
            {
                continue;
            }

            if (kind == SealKind.Dataset)
            {
                AtomicFile.Write(local, output =>
                {
                    using var opened = _box.Open(kind, File.OpenRead(local), id.ToByteArray());
                    _box.Seal(kind, opened, output, id.ToByteArray());
                }, overwrite: true);
            }
            else
            {
                var plain = _box.Open(kind, File.ReadAllBytes(local), id.ToByteArray());
                AtomicFile.Write(local, _box.Seal(kind, plain, id.ToByteArray()), overwrite: true);
            }

            resealed++;
        }

        if (resealed > 0)
        {
            _log.Info(Category, $"Vault {Id}: {resealed} files sealed again with the key of epoch {_box.Keys.CurrentEpoch}");
        }

        return resealed;
    }

    private static SealKind? KindOf(string path) => Path.GetExtension(path) switch
    {
        VaultFolder.ChangeExtension => SealKind.Change,
        VaultFolder.ContentExtension => SealKind.Note,
        VaultFolder.ThumbnailExtension => SealKind.Thumbnail,
        VaultFolder.DatasetExtension => SealKind.Dataset,
        _ => null,
    };

    private static int? EpochOf(string path)
    {
        var start = new byte[SealKind.HeaderSize];
        using var file = File.OpenRead(path);
        return SealKind.EpochOf(start.AsSpan(0, file.ReadAtLeast(start, start.Length, throwOnEndOfStream: false)));
    }
}
