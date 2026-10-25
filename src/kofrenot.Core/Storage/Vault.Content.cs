using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Server;

namespace Kofrenot.Core.Storage;

/// <summary>
/// Content on demand: a server vault downloads a note's or dataset's file when it is needed, and a
/// thumbnail missing on this device (it comes with the sync otherwise) when it is shown.
/// </summary>
public sealed partial class Vault
{
    // Downloads end on the thread that awaits them, which may not be the vault's: these are locked.
    // Keyed by the file's path in the vault: a dataset version's file and thumbnail share its id.
    private readonly Lock _fetchLock = new();
    private readonly Dictionary<string, Task<bool>> _fetching = [];
    private readonly Dictionary<string, ServerFailure> _fetchFailures = [];

    /// <summary>Raised when a download of content ends (<see cref="FetchAsync"/>), whether it went through or not.</summary>
    public event Action? ContentFetched;

    /// <summary>The vault downloads content when it is needed (a server vault); a folder vault has all of it, once synced.</summary>
    public bool FetchesContent => _fetcher is not null;

    /// <summary>Downloads of content are under way.</summary>
    public bool IsFetching
    {
        get
        {
            lock (_fetchLock)
            {
                return _fetching.Count > 0;
            }
        }
    }

    /// <summary>Where the content of a note's or dataset's version stands on this device.</summary>
    public ContentState StateOf(Guid version)
    {
        if (Notes.Exists(version) || Datasets.Exists(version))
        {
            return ContentState.Here;
        }

        string[] paths = [PathOf(VaultFolder.ContentFileOf(_folder, version)), PathOf(VaultFolder.DatasetFileOf(_folder, version))];
        lock (_fetchLock)
        {
            return paths.Any(_fetching.ContainsKey) ? ContentState.Downloading
                : paths.Select(path => _fetchFailures.TryGetValue(path, out var failure) ? failure : (ServerFailure?)null).FirstOrDefault(failure => failure is not null) is { } failed
                    ? new ContentState(ContentLevel.Failed, failed)
                    : ContentState.NotHere;
        }
    }

    /// <summary>
    /// A note version's content, or null when its file is not here: a server vault then downloads
    /// it (<see cref="FetchAsync"/>), and <see cref="ContentFetched"/> says when to read it again.
    /// </summary>
    public string? ReadNote(Guid version)
    {
        if (Notes.Read(version) is { } content)
        {
            AlreadyHere(version);
            return content;
        }

        _ = Fetch(new Wanted(version, VaultFolder.ContentFileOf(_folder, version), "Content"), retry: false, CancellationToken.None);
        return null;
    }

    /// <summary>
    /// A dataset version's thumbnail, or null when its file is not here: a server vault then
    /// downloads it, as <see cref="ReadNote"/> does. One the server does not have (an image that
    /// could not be decoded) is not asked for again.
    /// </summary>
    public byte[]? ReadThumbnail(Guid version)
    {
        if (Thumbnails.Read(version) is { } png)
        {
            return png;
        }

        _ = Fetch(new Wanted(version, VaultFolder.ThumbnailFileOf(_folder, version), "Thumbnail"), retry: false, CancellationToken.None);
        return null;
    }

    /// <summary>
    /// Makes a version's content readable: true when its file is here, or once downloaded (a server
    /// vault). A version being downloaded is waited for, not downloaded twice. One whose download
    /// failed is tried again only when <paramref name="retry"/> (the user asks for it), or after
    /// <see cref="RetryFailed"/>. Why it failed: <see cref="StateOf"/>.
    /// </summary>
    public Task<bool> FetchAsync(ContentEdited version, bool retry = false, CancellationToken cancel = default)
    {
        var file = version is DatasetEdited ? VaultFolder.DatasetFileOf(_folder, version.Id) : VaultFolder.ContentFileOf(_folder, version.Id);
        if (File.Exists(file))
        {
            AlreadyHere(version.Id);
            return Task.FromResult(true);
        }

        return Fetch(new Wanted(version.Id, file, "Content"), retry, cancel);
    }

    /// <summary>
    /// Lets the downloads that failed be tried again (a sync went through), but for thumbnails the
    /// server does not have. False when none had failed.
    /// </summary>
    public bool RetryFailed()
    {
        lock (_fetchLock)
        {
            var retried = _fetchFailures.Where(failed => !IsMissingThumbnail(failed.Key, failed.Value)).Select(failed => failed.Key).ToList();
            foreach (var path in retried)
            {
                _fetchFailures.Remove(path);
            }

            return retried.Count > 0;
        }
    }

    private static bool IsMissingThumbnail(string path, ServerFailure failure) =>
        failure == ServerFailure.NotFound && path.EndsWith(VaultFolder.ThumbnailExtension, StringComparison.Ordinal);

    // Only a server vault says so: a folder vault always has its content, once synced.
    private void AlreadyHere(Guid version)
    {
        if (_fetcher is not null)
        {
            _log.Debug(Category, $"Content {version} already here");
        }
    }

    private string PathOf(string file) => StorePaths.PathOf(_folder, file);

    private Task<bool> Fetch(Wanted wanted, bool retry, CancellationToken cancel)
    {
        if (_fetcher is not { } fetcher)
        {
            return Task.FromResult(false);
        }

        var path = PathOf(wanted.File);
        lock (_fetchLock)
        {
            if (_fetching.TryGetValue(path, out var running))
            {
                return running;
            }

            if (!retry && _fetchFailures.ContainsKey(path))
            {
                return Task.FromResult(false);
            }

            _fetchFailures.Remove(path);
            var fetch = DownloadAsync(fetcher, wanted, path, cancel);
            _fetching[path] = fetch;
            return fetch;
        }
    }

    private async Task<bool> DownloadAsync(IContentFetcher fetcher, Wanted wanted, string path, CancellationToken cancel)
    {
        // Registered first, by Fetch: the download starts once that is done.
        await Task.Yield();
        var failure = (ServerFailure?)null;
        try
        {
            await fetcher.FetchAsync(path, cancel);
            _log.Debug(Category, $"{wanted.What} {wanted.Version} downloaded");
            return true;
        }
        catch (ServerException ex)
        {
            failure = ex.Failure;
            var expected = ex.Failure == ServerFailure.Offline || IsMissingThumbnail(path, ex.Failure);
            _log.Write(LogEntry.Create(expected ? LogLevel.Debug : LogLevel.Warning, Category, $"{wanted.What} {wanted.Version} not downloaded: {ex.Message}"));
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            failure = ServerFailure.Failed;
            _log.Warn(Category, $"{wanted.What} {wanted.Version} not downloaded: {ex.Message}");
            return false;
        }
        finally
        {
            lock (_fetchLock)
            {
                _fetching.Remove(path);
                if (failure is { } failed)
                {
                    _fetchFailures[path] = failed;
                }
            }

            ContentFetched?.Invoke();
        }
    }

    /// <summary>A file of a version to download, and what it is, for the log ("Content", "Thumbnail").</summary>
    private sealed record Wanted(Guid Version, string File, string What);
}
