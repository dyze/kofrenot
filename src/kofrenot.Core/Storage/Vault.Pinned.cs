using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Storage;

/// <summary>
/// Pinned for offline: a server vault downloads the content of its pinned nodes ahead (their notes'
/// text and datasets' files, the current versions), so it is readable without the server.
/// </summary>
public sealed partial class Vault
{
    /// <summary>The current content versions of the pinned nodes.</summary>
    public IReadOnlyList<ContentEdited> PinnedContent =>
        Graph.PinnedNodes.SelectMany(node => ContentOf(node.Id)).ToList();

    /// <summary>Every current content version of <paramref name="node"/> is on this device.</summary>
    public bool HasAllContent(Guid node) => ContentOf(node).All(HasContent);

    /// <summary>What a pinned node's pin says: in a server vault, whether its content is kept on this device yet.</summary>
    public string PinnedLabel(Guid node) =>
        !FetchesContent ? "Pinned"
        : HasAllContent(node) ? "Pinned, kept offline"
        : "Pinned, kept offline once downloaded";

    /// <summary>
    /// Downloads the content of the pinned nodes that is not here yet, one file at a time; a
    /// download that failed is tried again after <see cref="RetryFailed"/>. How many were downloaded.
    /// </summary>
    public async Task<int> KeepPinnedAsync(CancellationToken cancel = default)
    {
        if (!FetchesContent)
        {
            return 0;
        }

        var downloaded = 0;
        foreach (var version in PinnedContent.Where(version => !HasContent(version)))
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
