namespace Kofrenot.Core.Graph;

/// <summary>
/// A node type of the user's, as its latest current definition says. <see cref="Definitions"/>
/// holds several after changes made apart (a conflict); <see cref="Ids"/> are all its definitions:
/// what removes it.
/// </summary>
public sealed record CustomNodeType(NodeType Type, IReadOnlyList<NodeTypeDefined> Definitions, IReadOnlyList<Guid> Ids)
{
    public bool IsConflict => Definitions.Count > 1;
}

/// <summary>
/// The node types of a vault: the built-in ones, then the user's, oldest first. A definition of a
/// built-in type's id is ignored. Built again after each change.
/// </summary>
public sealed class CustomNodeTypes
{
    private readonly IReadOnlyList<NodeType> _builtIn;
    private readonly ISet<Guid> _removed;
    private readonly Dictionary<Guid, NodeTypeDefined> _definitions = [];
    private (IReadOnlyList<NodeType> Types, List<CustomNodeType> All)? _built;

    internal CustomNodeTypes(IReadOnlyList<NodeType> builtIn, ISet<Guid> removed)
    {
        _builtIn = builtIn;
        _removed = removed;
    }

    /// <summary>The built-in node types, then the user's.</summary>
    public IReadOnlyList<NodeType> Types => Build().Types;

    /// <summary>The user's node types not removed, oldest first.</summary>
    public IReadOnlyList<CustomNodeType> All => Build().All;

    /// <summary>The user's node type with this id, or null.</summary>
    public CustomNodeType? Find(string id) => All.FirstOrDefault(custom => custom.Type.Id == id);

    internal bool Add(NodeTypeDefined definition)
    {
        var added = _definitions.TryAdd(definition.Id, definition);
        if (added)
        {
            Changed();
        }

        return added;
    }

    /// <summary>Called when something was removed: the types are built again.</summary>
    internal void Changed() => _built = null;

    private (IReadOnlyList<NodeType> Types, List<CustomNodeType> All) Build()
    {
        if (_built is { } built)
        {
            return built;
        }

        var all = _definitions.Values
            .Where(definition => !_removed.Contains(definition.Id) && _builtIn.All(type => type.Id != definition.NodeType))
            .GroupBy(definition => definition.NodeType)
            .OrderBy(definitions => definitions.Min(definition => definition.At))
            .ThenBy(definitions => definitions.Key, StringComparer.Ordinal)
            .Select(definitions =>
            {
                var current = CurrentVersions.Of(definitions);
                var shown = current[^1];
                var type = new NodeType(definitions.Key, shown.Name, shown.Fields, shown.Icon, NodeColorNames.Parse(shown.Color));
                return new CustomNodeType(type, current, definitions.Select(definition => definition.Id).ToList());
            })
            .ToList();
        IReadOnlyList<NodeType> types = all.Count == 0 ? _builtIn : [.. _builtIn, .. all.Select(custom => custom.Type)];
        _built = (types, all);
        return (types, all);
    }
}
