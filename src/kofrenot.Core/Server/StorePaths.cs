using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Server;

/// <summary>
/// The paths of a vault's files in a store (<c>changes/3f/3f9c….kfc</c>), and where they are in
/// its mirror. Only paths a vault writes are accepted, so a store cannot write outside the mirror.
/// </summary>
internal static class StorePaths
{
    private const int MaxSegments = 8;

    public static bool IsValid(string path)
    {
        var segments = path.Split('/');
        return segments.Length <= MaxSegments && segments.All(IsValidSegment);
    }

    /// <summary>
    /// A note's or dataset's file: left on the store by a sync, downloaded when needed
    /// (<see cref="ContentFetcher"/>). Thumbnails come with the sync: small, and shown in lists.
    /// </summary>
    public static bool IsOnDemand(string path) =>
        path.StartsWith(VaultFolder.ContentFolder + "/", StringComparison.Ordinal)
        && (path.EndsWith(VaultFolder.ContentExtension, StringComparison.Ordinal) || path.EndsWith(VaultFolder.DatasetExtension, StringComparison.Ordinal));

    /// <summary>Downloads a file of the store under a temporary name, then renames it: the vault sees the whole file or nothing.</summary>
    /// <exception cref="ServerException">The server failed or cannot be reached; no file is left.</exception>
    public static async Task DownloadAsync(IStoreFiles files, Guid store, string mirror, string path, CancellationToken cancel)
    {
        var local = LocalOf(mirror, path);
        Directory.CreateDirectory(Path.GetDirectoryName(local)!);
        var temporary = local + AtomicFile.TemporaryExtension;
        try
        {
            await using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await files.DownloadAsync(store, path, output, cancel);
            }

            File.Move(temporary, local);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public static string LocalOf(string mirror, string path) => Path.Combine([mirror, .. path.Split('/')]);

    public static string PathOf(string mirror, string file) => Path.GetRelativePath(mirror, file).Replace(Path.DirectorySeparatorChar, '/');

    private static bool IsValidSegment(string segment) =>
        segment is { Length: > 0 and <= 128 } and not "." and not ".."
        && segment.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');
}
