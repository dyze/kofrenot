namespace Kofrenot.Core.Graph;

/// <summary>
/// A type a node has, and the change that gave it (<see cref="Id"/>): the node's id for the type it
/// was added with, a retype's, or a <see cref="NodeTypeAdded"/>'s.
/// </summary>
public sealed record TypeVersion(Guid Id, string Type, DateTimeOffset At);

/// <summary>
/// The types of every node, a set. A node starts with the type it was added with, which retypes
/// (before format 8) replace (<see cref="CurrentVersions"/>: two made apart are both kept); then
/// <see cref="NodeTypeAdded"/> adds types and <see cref="NodeTypeRemoved"/> takes them out, by the
/// changes that gave them, so the result does not depend on the order they arrive in.
/// </summary>
internal sealed class NodeTypeSets
{
    private readonly Dictionary<Guid, Dictionary<Guid, Change>> _byNode = [];

    /// <summary>False when this change is already there.</summary>
    public bool Add(NodeRetyped retype) => Add(retype.Node, retype);

    /// <summary>False when this change is already there.</summary>
    public bool Add(NodeTypeAdded added) => Add(added.Node, added);

    /// <summary>False when this change is already there.</summary>
    public bool Add(NodeTypeRemoved removed) => Add(removed.Node, removed);

    /// <summary>The changes of a node's types, whatever they replace or remove: when it last changed.</summary>
    public IEnumerable<Change> Of(Guid node) => _byNode.TryGetValue(node, out var changes) ? changes.Values : [];

    /// <summary>The types of <paramref name="added"/>, each with the change that gave it, oldest first; one type may be given twice.</summary>
    public IReadOnlyList<TypeVersion> Current(Node added)
    {
        var changes = Of(added.Id).ToList();
        var removed = changes.OfType<NodeTypeRemoved>().SelectMany(removal => removal.Removes).ToHashSet();
        var startTypes = CurrentVersions.Of(changes.OfType<NodeRetyped>()
                .Select(retype => new Typed(retype.Id, retype.At, retype.Type, retype.Replaces))
                .Prepend(new Typed(added.Id, added.Created, added.Type, [])))
            .Select(typed => new TypeVersion(typed.Id, typed.Type, typed.At));
        return startTypes
            .Concat(changes.OfType<NodeTypeAdded>().Select(type => new TypeVersion(type.Id, type.Type, type.At)))
            .Where(type => !removed.Contains(type.Id))
            .OrderBy(type => type.At)
            .ThenBy(type => type.Id)
            .ToList();
    }

    /// <summary>
    /// The type shown: its type with content (a note, a dataset or a view) if it has one, else its
    /// latest; <see cref="TypeCatalog.Generic"/> when it has none left.
    /// </summary>
    public string Shown(Node added, Func<string, bool> hasContent)
    {
        var types = Current(added);
        return types.LastOrDefault(type => hasContent(type.Type))?.Type ?? types.LastOrDefault()?.Type ?? TypeCatalog.Generic.Id;
    }

    private bool Add(Guid node, Change change)
    {
        if (!_byNode.TryGetValue(node, out var changes))
        {
            _byNode[node] = changes = [];
        }

        return changes.TryAdd(change.Id, change);
    }

    private sealed record Typed(Guid Id, DateTimeOffset At, string Type, IReadOnlyList<Guid> Replaces) : IReplacing;
}
