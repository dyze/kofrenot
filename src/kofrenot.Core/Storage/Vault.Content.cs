using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Server;

namespace Kofrenot.Core.Storage;

/// <summary>Content on demand: a server vault downloads a note's or dataset's file when it is needed.</summary>
public sealed partial class Vault
{
    // Downloads end on the thread that awaits them, which may not be the vault's: these are locked.
    private readonly Lock _fetchLock = new();
    private readonly Dictionary<Guid, Task<bool>> _fetching = [];
    private readonly Dictionary<Guid, ServerFailure> _fetchFailures = [];

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

        lock (_fetchLock)
        {
            return _fetching.ContainsKey(version) ? ContentState.Downloading
                : _fetchFailures.TryGetValue(version, out var failure) ? new ContentState(ContentLevel.Failed, failure)
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
            return content;
        }

        _ = Fetch(version, VaultFolder.ContentFileOf(_folder, version), retry: false, CancellationToken.None);
        return null;
    }

    /// <summary>
    /// Makes a version's content readable: true when its file is here, or once downloaded (a server
    /// vault). A version being downloaded is waited for, not downloaded twice. One whose download
    /// failed is tried again only when <paramref name="retry"/> (the user asks for it), or after
    /// <see cref="RetryFailed"/>. Why it failed: <see cref="StateOf"/>.
    /// </summary>
    public Task<bool> FetchAsync(ContentEdited version, bool retry = false, CancellationToken cancel = default) =>
        Fetch(version.Id, version is DatasetEdited ? VaultFolder.DatasetFileOf(_folder, version.Id) : VaultFolder.ContentFileOf(_folder, version.Id), retry, cancel);

    /// <summary>Lets the downloads that failed be tried again (a sync went through). False when none had.</summary>
    public bool RetryFailed()
    {
        lock (_fetchLock)
        {
            var any = _fetchFailures.Count > 0;
            _fetchFailures.Clear();
            return any;
        }
    }

    private Task<bool> Fetch(Guid version, string file, bool retry, CancellationToken cancel)
    {
        if (File.Exists(file))
        {
            return Task.FromResult(true);
        }

        if (_fetcher is not { } fetcher)
        {
            return Task.FromResult(false);
        }

        lock (_fetchLock)
        {
            if (_fetching.TryGetValue(version, out var running))
            {
                return running;
            }

            if (!retry && _fetchFailures.ContainsKey(version))
            {
                return Task.FromResult(false);
            }

            _fetchFailures.Remove(version);
            var fetch = DownloadAsync(fetcher, version, StorePaths.PathOf(_folder, file), cancel);
            _fetching[version] = fetch;
            return fetch;
        }
    }

    private async Task<bool> DownloadAsync(IContentFetcher fetcher, Guid version, string path, CancellationToken cancel)
    {
        // Registered first, by Fetch: the download starts once that is done.
        await Task.Yield();
        var failure = (ServerFailure?)null;
        try
        {
            await fetcher.FetchAsync(path, cancel);
            _log.Debug(Category, $"Content {version} downloaded");
            return true;
        }
        catch (ServerException ex)
        {
            failure = ex.Failure;
            _log.Write(LogEntry.Create(ex.Failure == ServerFailure.Offline ? LogLevel.Debug : LogLevel.Warning, Category, $"Content {version} not downloaded: {ex.Message}"));
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            failure = ServerFailure.Failed;
            _log.Warn(Category, $"Content {version} not downloaded: {ex.Message}");
            return false;
        }
        finally
        {
            lock (_fetchLock)
            {
                _fetching.Remove(version);
                if (failure is { } failed)
                {
                    _fetchFailures[version] = failed;
                }
            }

            ContentFetched?.Invoke();
        }
    }
}
