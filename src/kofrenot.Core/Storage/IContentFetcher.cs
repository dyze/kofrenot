namespace Kofrenot.Core.Storage;

/// <summary>
/// Downloads a vault's content files when they are needed: a server vault's notes and datasets
/// are left on the server by its sync (<see cref="Vault.FetchAsync"/>).
/// </summary>
public interface IContentFetcher
{
    /// <summary>Downloads the file at <paramref name="path"/> (its path in the vault: <c>content/ab/ab12….kfn</c>) into the vault's folder.</summary>
    /// <exception cref="Server.ServerException">The server failed, cannot be reached, or the user is signed out.</exception>
    /// <exception cref="IOException">The file cannot be written.</exception>
    Task FetchAsync(string path, CancellationToken cancel = default);
}
