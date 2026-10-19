namespace Kofrenot.Core.Graph;

/// <summary>
/// The content versions of every note, or of every dataset, and each node's current ones (<see cref="CurrentVersions"/>).
/// </summary>
internal sealed class ContentVersions<T>
    where T : ContentEdited
{
    private readonly Dictionary<Guid, Dictionary<Guid, T>> _byNode = [];

    /// <summary>False when this version is already there.</summary>
    public bool Add(T version)
    {
        if (!_byNode.TryGetValue(version.Node, out var versions))
        {
            _byNode[version.Node] = versions = [];
        }

        return versions.TryAdd(version.Id, version);
    }

    /// <summary>The current versions of a node, oldest first; empty before its first save.</summary>
    public IReadOnlyList<T> Current(Guid node) =>
        _byNode.TryGetValue(node, out var versions) ? CurrentVersions.Of(versions.Values) : [];
}
