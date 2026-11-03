namespace Kofrenot.Core.Graph;

/// <summary>
/// The positions of nodes moved by hand in each graph view (<see cref="NodesPlaced"/>): for each node,
/// the newest one not removed. The result does not depend on the order the changes arrive in.
/// </summary>
internal sealed class NodePlacements(ISet<Guid> removed)
{
    private readonly Dictionary<Guid, Dictionary<Guid, NodesPlaced>> _byView = [];

    /// <summary>False when it is already there.</summary>
    public bool Add(NodesPlaced placed)
    {
        if (!_byView.TryGetValue(placed.View, out var changes))
        {
            _byView[placed.View] = changes = [];
        }

        return changes.TryAdd(placed.Id, placed);
    }

    /// <summary>The placements of a view not removed, oldest first.</summary>
    public IReadOnlyList<NodesPlaced> Of(Guid view) => _byView.TryGetValue(view, out var changes)
        ? changes.Values.Where(change => !removed.Contains(change.Id)).OrderBy(change => change.At).ThenBy(change => change.Id).ToList()
        : [];

    /// <summary>Each node's position in a view: the newest one.</summary>
    public IReadOnlyDictionary<Guid, NodePosition> PositionsIn(Guid view)
    {
        var positions = new Dictionary<Guid, NodePosition>();
        foreach (var position in Of(view).SelectMany(change => change.Positions))
        {
            positions[position.Node] = position;
        }

        return positions;
    }
}
