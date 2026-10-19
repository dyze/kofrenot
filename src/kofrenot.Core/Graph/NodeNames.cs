namespace Kofrenot.Core.Graph;

/// <summary>
/// The names of one node: the name it was added with, then its renames. Its current names are
/// those no rename replaces (<see cref="CurrentVersions"/>): one normally, several after renames
/// made apart (a conflict).
/// </summary>
internal sealed class NodeNames
{
    private readonly Dictionary<Guid, NodeRenamed> _renames = [];

    /// <summary>The node as added; null while only renames of it have arrived.</summary>
    public Node? Added { get; set; }

    public IEnumerable<NodeRenamed> Renames => _renames.Values;

    /// <summary>False when this rename is already there.</summary>
    public bool Add(NodeRenamed rename) => _renames.TryAdd(rename.Id, rename);

    /// <summary>The current names, oldest first.</summary>
    public IReadOnlyList<NameVersion> Current()
    {
        var versions = _renames.Values.Select(rename => new Named(rename.Id, rename.At, rename.Name, rename.Replaces));
        if (Added is { } node)
        {
            versions = versions.Prepend(new Named(node.Id, node.Created, node.Name, []));
        }

        return CurrentVersions.Of(versions).Select(named => new NameVersion(named.Id, named.Name, named.At)).ToList();
    }

    /// <summary>The name shown: the latest current one.</summary>
    public string Shown(Node added) => Current().LastOrDefault()?.Name ?? added.Name;

    private sealed record Named(Guid Id, DateTimeOffset At, string Name, IReadOnlyList<Guid> Replaces) : IReplacing;
}
