namespace Kofrenot.Core.Graph;

/// <summary>
/// The names of one node: the name it was added with, then its renames. Its current names are
/// those no rename replaces: one normally, several after renames made apart (a conflict). The
/// result does not depend on the order the changes arrive in.
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
        var replaced = _renames.Values.SelectMany(rename => rename.Replaces).ToHashSet();
        var versions = _renames.Values.Select(rename => new NameVersion(rename.Id, rename.Name, rename.At));
        if (Added is { } node)
        {
            versions = versions.Prepend(new NameVersion(node.Id, node.Name, node.Created));
        }

        return versions
            .Where(version => !replaced.Contains(version.Id))
            .OrderBy(version => version.At)
            .ThenBy(version => version.Id)
            .ToList();
    }

    /// <summary>The name shown: the latest current one.</summary>
    public string Shown(Node added) => Current().LastOrDefault()?.Name ?? added.Name;
}
