using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Storage;

/// <summary>
/// Kept offline: a server vault downloads ahead the content of the nodes this device keeps offline
/// (their notes' text and datasets' files, the current versions), so it is readable without the server.
/// </summary>
public sealed partial class Vault
{
    /// <summary>The current content versions of <paramref name="nodes"/>.</summary>
    public IReadOnlyList<ContentEdited> ContentOf(IEnumerable<Guid> nodes) => nodes.SelectMany(ContentOf).ToList();

    /// <summary>Every current content version of <paramref name="node"/> is on this device.</summary>
    public bool HasAllContent(Guid node) => ContentOf(node).All(HasContent);

    /// <summary>What a node kept offline says: whether its content is on this device yet.</summary>
    public string OfflineLabel(Guid node) => HasAllContent(node) ? "Kept offline" : "Kept offline once downloaded";

    /// <summary>
    /// Downloads the content of <paramref name="nodes"/> that is not here yet, one file at a time;
    /// a download that failed is tried again after <see cref="RetryFailed"/>. How many were downloaded.
    /// </summary>
    public async Task<int> KeepOfflineAsync(IEnumerable<Guid> nodes, CancellationToken cancel = default)
    {
        if (!FetchesContent)
        {
            return 0;
        }

        var downloaded = 0;
        foreach (var version in ContentOf(nodes).Where(version => !HasContent(version)))
        {
            if (await FetchAsync(version, retry: false, cancel))
            {
                downloaded++;
            }
        }

        return downloaded;
    }

    private IEnumerable<ContentEdited> ContentOf(Guid node) =>
        Graph.NoteVersionsOf(node).Cast<ContentEdited>().Concat(Graph.DatasetVersionsOf(node));
}
