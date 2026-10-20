namespace Kofrenot.Core.Graph;

/// <summary>
/// A type a node has: the type it was added with (<see cref="Id"/> is then the node's id), or a
/// retype (<see cref="Id"/> is the change's).
/// </summary>
public sealed record TypeVersion(Guid Id, string Type, DateTimeOffset At);

/// <summary>
/// The retypes of every node. A node's current types are those no retype replaces
/// (<see cref="CurrentVersions"/>), the type it was added with included: one normally, several
/// after retypes made apart (a conflict).
/// </summary>
internal sealed class NodeTypes
{
    private readonly Dictionary<Guid, Dictionary<Guid, NodeRetyped>> _byNode = [];

    /// <summary>False when this retype is already there.</summary>
    public bool Add(NodeRetyped retype)
    {
        if (!_byNode.TryGetValue(retype.Node, out var retypes))
        {
            _byNode[retype.Node] = retypes = [];
        }

        return retypes.TryAdd(retype.Id, retype);
    }

    /// <summary>The retypes of a node, whatever they replace.</summary>
    public IEnumerable<NodeRetyped> Of(Guid node) => _byNode.TryGetValue(node, out var retypes) ? retypes.Values : [];

    /// <summary>The current types of <paramref name="added"/>, oldest first.</summary>
    public IReadOnlyList<TypeVersion> Current(Node added) =>
        CurrentVersions.Of(Of(added.Id)
                .Select(retype => new Typed(retype.Id, retype.At, retype.Type, retype.Replaces))
                .Prepend(new Typed(added.Id, added.Created, added.Type, [])))
            .Select(typed => new TypeVersion(typed.Id, typed.Type, typed.At))
            .ToList();

    /// <summary>The type shown: the latest current one.</summary>
    public string Shown(Node added) => Current(added)[^1].Type;

    private sealed record Typed(Guid Id, DateTimeOffset At, string Type, IReadOnlyList<Guid> Replaces) : IReplacing;
}
