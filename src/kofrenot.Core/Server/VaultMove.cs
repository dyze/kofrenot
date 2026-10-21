using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Server;

/// <summary>Moves a folder vault to a server: its files, as they are, into a new store's mirror.</summary>
public static class VaultMove
{
    /// <summary>
    /// Copies the vault's files into <paramref name="mirror"/>; the next sync uploads them. Files
    /// a vault does not write (a sync tool's own, half-written ones) stay behind. The folder is
    /// not changed.
    /// </summary>
    /// <returns>The number of files copied.</returns>
    /// <exception cref="IOException">The folder is not a vault, the mirror is not empty, or a file cannot be copied.</exception>
    public static int CopyToMirror(string folder, string mirror)
    {
        if (VaultFolder.Inspect(folder) != FolderKind.Vault)
        {
            throw new IOException("Only a vault can be moved to a server");
        }

        if (VaultFolder.Inspect(mirror) != FolderKind.Empty)
        {
            throw new IOException("A vault can only be moved into an empty mirror");
        }

        var copied = 0;
        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            var path = StorePaths.PathOf(folder, file);
            if (StorePaths.IsValid(path) && !path.EndsWith(AtomicFile.TemporaryExtension, StringComparison.OrdinalIgnoreCase))
            {
                var target = StorePaths.LocalOf(mirror, path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
                copied++;
            }
        }

        return copied;
    }
}
