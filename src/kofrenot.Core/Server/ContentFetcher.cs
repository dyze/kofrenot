using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Server;

/// <summary>
/// Downloads a server vault's content files into its mirror when they are needed, with the
/// account's client of the moment (<paramref name="files"/>: null while signed out).
/// </summary>
public sealed class ContentFetcher(string mirror, Guid store, Func<IStoreFiles?> files, ILog? log = null) : IContentFetcher
{
    private readonly ILog _log = log ?? NullLog.Instance;

    public async Task FetchAsync(string path, CancellationToken cancel = default)
    {
        if (!StorePaths.IsValid(path))
        {
            throw new ArgumentException($"Not a path of a vault: {path}", nameof(path));
        }

        if (files() is not { } client)
        {
            throw new ServerException(ServerFailure.SignInNeeded);
        }

        await StorePaths.DownloadAsync(client, store, mirror, path, cancel);
        _log.Debug("Sync", $"Store {store}: {path} downloaded on demand");
    }
}
