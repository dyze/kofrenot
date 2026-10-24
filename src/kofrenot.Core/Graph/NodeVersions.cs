namespace Kofrenot.Core.Graph;

/// <summary>A version of something a node has, replacing earlier ones: a content version, a colour.</summary>
internal interface INodeVersion : IReplacing
{
    Guid Node { get; }
}

/// <summary>
/// The versions of one kind (a note's content, a dataset's file, a view's definition, a colour)
/// of every node, and each node's current ones (<see cref="CurrentVersions"/>).
/// </summary>
internal sealed class NodeVersions<T>
    where T : INodeVersion
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

    /// <summary>The versions of a node, whatever they replace.</summary>
    public IEnumerable<T> Of(Guid node) => _byNode.TryGetValue(node, out var versions) ? versions.Values : [];

    /// <summary>The current versions of a node, oldest first; empty before its first one.</summary>
    public IReadOnlyList<T> Current(Guid node) => CurrentVersions.Of(Of(node));
}
