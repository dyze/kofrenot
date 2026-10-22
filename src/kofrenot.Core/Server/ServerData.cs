namespace Kofrenot.Core.Server;

/// <summary>A signed-in session: a short-lived access token, and the refresh token that renews it.</summary>
public sealed record ServerSession(string AccessToken, DateTimeOffset AccessExpires, string RefreshToken);

public enum StoreRole
{
    Reader,
    Writer,
    Owner,
}

/// <summary>A store on the server, as listed for the signed-in account.</summary>
public sealed record ServerStore(Guid Id, StoreRole Role, int Files, long Bytes, long? Fence = null);

/// <summary>A member of a store, by account.</summary>
public sealed record StoreMember(Guid Account, StoreRole Role);

/// <summary>An account found by email: its id, and the public part of its data (none before it has an identity).</summary>
public sealed record FoundAccount(Guid Id, byte[]? Public);

/// <summary>The <paramref name="Seq"/>-th file added to a store.</summary>
public sealed record FeedEntry(long Seq, string Path, long Size);

/// <summary>A page of a store's feed, and whether more entries follow.</summary>
public sealed record FeedPage(IReadOnlyList<FeedEntry> Entries, bool More);

/// <summary>The files of the server's stores: what <see cref="ServerSync"/> needs.</summary>
public interface IStoreFiles
{
    /// <exception cref="ServerException">The call failed.</exception>
    Task<FeedPage> FeedAsync(Guid store, long since, CancellationToken cancel = default);

    /// <exception cref="ServerException">The call failed.</exception>
    Task DownloadAsync(Guid store, string path, Stream destination, CancellationToken cancel = default);

    /// <summary>
    /// Uploads a file, read from <paramref name="open"/> (opened again if the call is retried),
    /// naming <paramref name="fence"/>: the store refuses it below its fence, if it has one.
    /// </summary>
    /// <exception cref="ServerException">The call failed.</exception>
    Task<UploadResult> UploadAsync(Guid store, string path, Func<Stream> open, long fence, CancellationToken cancel = default);
}

/// <summary>A server account's data, opaque to the server: a public part any account reads, a private part only its own; its version counts the writes (0: none yet).</summary>
public sealed record AccountData(long Version, byte[]? Public, byte[]? Private);

/// <summary>What became of an upload.</summary>
public enum UploadResult
{
    Uploaded,

    /// <summary>The store had this file already.</summary>
    Exists,

    /// <summary>Below the store's fence: sealed with a key older than its latest, to seal again.</summary>
    Fenced,
}
