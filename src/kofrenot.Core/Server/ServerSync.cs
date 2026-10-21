using System.Text.Json;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Server;

/// <summary>What a sync did.</summary>
public sealed record SyncResult(int Downloaded, int Uploaded);

/// <summary>
/// Keeps a server vault's mirror (a local folder with the vault layout) and its store alike.
/// Files are only ever added on both sides, so a sync is two copies: the store's files added
/// since the last sync (its feed) are downloaded into the mirror, then the mirror's files the
/// store lacks are uploaded: those written while offline wait in the mirror until then. Its
/// state (the feed position, the files both sides have) is kept in <paramref name="stateFile"/>.
/// One sync at a time per mirror.
/// </summary>
public sealed class ServerSync(string mirror, string stateFile, Guid store, IStoreFiles files, ILog? log = null)
{
    private const string Category = "Sync";
    private const int SaveEvery = 50;

    private readonly ILog _log = log ?? NullLog.Instance;

    public Guid Store => store;

    public string Mirror => mirror;

    /// <exception cref="ServerException">The server failed or cannot be reached; what was done so far is kept.</exception>
    /// <exception cref="IOException">A file of the mirror cannot be read or written.</exception>
    public async Task<SyncResult> RunAsync(CancellationToken cancel = default)
    {
        var state = SyncState.Load(stateFile);
        Directory.CreateDirectory(mirror);
        try
        {
            var downloaded = await DownloadAsync(state, cancel);
            var uploaded = await UploadAsync(state, cancel);
            if (downloaded + uploaded > 0)
            {
                _log.Info(Category, $"Store {store}: {downloaded} files downloaded, {uploaded} uploaded");
            }

            return new SyncResult(downloaded, uploaded);
        }
        finally
        {
            state.Save(stateFile);
        }
    }

    /// <summary>The mirror's files the store does not have yet, as far as the last sync knows.</summary>
    public static int Waiting(string mirror, string stateFile) => FilesToUpload(mirror, SyncState.Load(stateFile)).Count;

    private async Task<int> DownloadAsync(SyncState state, CancellationToken cancel)
    {
        var downloaded = 0;
        FeedPage page;
        do
        {
            page = await files.FeedAsync(store, state.Seq, cancel);
            foreach (var entry in page.Entries)
            {
                if (!state.Known.Contains(entry.Path))
                {
                    if (!StorePaths.IsValid(entry.Path))
                    {
                        _log.Warn(Category, $"Store {store}: file {entry.Seq} skipped, its path is not one of a vault");
                    }
                    else if (!File.Exists(StorePaths.LocalOf(mirror, entry.Path)))
                    {
                        await DownloadFileAsync(entry.Path, cancel);
                        downloaded++;
                    }

                    state.Known.Add(entry.Path);
                }

                state.Seq = entry.Seq;
            }

            state.Save(stateFile);
        }
        while (page.More && page.Entries.Count > 0);

        return downloaded;
    }

    /// <summary>Downloads under a temporary name, then renames: the vault sees the whole file or nothing.</summary>
    private async Task DownloadFileAsync(string path, CancellationToken cancel)
    {
        var local = StorePaths.LocalOf(mirror, path);
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

        _log.Debug(Category, $"Store {store}: {path} downloaded");
    }

    private async Task<int> UploadAsync(SyncState state, CancellationToken cancel)
    {
        var (uploaded, checkedFiles) = (0, 0);
        foreach (var path in FilesToUpload(mirror, state))
        {
            checkedFiles++;
            var local = StorePaths.LocalOf(mirror, path);
            if (await files.UploadAsync(store, path, () => File.OpenRead(local), cancel))
            {
                uploaded++;
                _log.Debug(Category, $"Store {store}: {path} uploaded");
            }

            state.Known.Add(path);
            if (checkedFiles % SaveEvery == 0)
            {
                state.Save(stateFile);
            }
        }

        return uploaded;
    }

    /// <summary>
    /// The mirror's files not known on the store, in the order a reader needs them: the vault's
    /// header first, then contents, then the changes that refer to them.
    /// </summary>
    private static List<string> FilesToUpload(string mirror, SyncState state) =>
        Directory.Exists(mirror)
            ? Directory.EnumerateFiles(mirror, "*", SearchOption.AllDirectories)
                .Where(file => !file.EndsWith(AtomicFile.TemporaryExtension, StringComparison.OrdinalIgnoreCase))
                .Select(file => StorePaths.PathOf(mirror, file))
                .Where(path => StorePaths.IsValid(path) && !state.Known.Contains(path))
                .OrderBy(UploadRank)
                .ThenBy(path => path, StringComparer.Ordinal)
                .ToList()
            : [];

    private static int UploadRank(string path) =>
        path == VaultFolder.HeaderFile ? 0
        : path.EndsWith(VaultFolder.ChangeExtension, StringComparison.Ordinal) ? 2
        : 1;

    /// <summary>A mirror's sync state, in a JSON file beside it.</summary>
    private sealed class SyncState
    {
        public long Seq { get; set; }

        public HashSet<string> Known { get; set; } = new(StringComparer.Ordinal);

        public static SyncState Load(string path)
        {
            try
            {
                return File.Exists(path) ? JsonSerializer.Deserialize<SyncState>(File.ReadAllText(path)) ?? new() : new();
            }
            catch (JsonException)
            {
                // A damaged state costs one full sync: every file is checked again, none copied twice.
                return new();
            }
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            AtomicFile.Write(path, JsonSerializer.SerializeToUtf8Bytes(this), overwrite: true);
        }
    }
}
