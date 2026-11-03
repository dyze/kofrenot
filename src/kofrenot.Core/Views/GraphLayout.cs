using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Views;

/// <summary>
/// Where a graph view's cards go: the nodes moved by hand where they were left, the others placed by
/// the view's layout (<see cref="LayeredLayout"/>, <see cref="NetworkLayout"/>), then pushed right
/// until they cover no other card. The same content gives the same places: nothing is random.
/// </summary>
public static class GraphLayout
{
    /// <summary>A card's size, in the view's units (pixels at zoom 1).</summary>
    public const double CardWidth = 180;

    public const double CardHeight = 72;

    /// <summary>The room between two cards side by side, and between two ranks.</summary>
    public const double ColumnGap = 40;

    public const double RankGap = 90;

    /// <summary>The centre of each node's card, by node id.</summary>
    /// <param name="moved">The positions of the nodes moved by hand (<see cref="KnowledgeGraph.PositionsIn"/>); those of nodes not shown are ignored.</param>
    public static IReadOnlyDictionary<Guid, NodePosition> Of(GraphContent content, GraphLayoutKind kind, IReadOnlyDictionary<Guid, NodePosition> moved)
    {
        var fixedPositions = content.Nodes.Where(node => moved.ContainsKey(node.Id)).ToDictionary(node => node.Id, node => moved[node.Id]);
        var placed = kind == GraphLayoutKind.Layered ? LayeredLayout.Place(content) : NetworkLayout.Place(content, fixedPositions);
        return WithoutOverlaps(content.Nodes, placed, fixedPositions);
    }

    /// <summary>Whether two cards, by their centres, cover one another (their gaps included).</summary>
    internal static bool Overlap(NodePosition first, NodePosition second) =>
        Math.Abs(first.X - second.X) < CardWidth + ColumnGap / 2 && Math.Abs(first.Y - second.Y) < CardHeight + ColumnGap / 2;

    // The fixed cards first, then each other one in order, moved right until it covers none placed.
    private static Dictionary<Guid, NodePosition> WithoutOverlaps(IReadOnlyList<Node> nodes, IReadOnlyDictionary<Guid, NodePosition> placed, IReadOnlyDictionary<Guid, NodePosition> fixedPositions)
    {
        var result = new Dictionary<Guid, NodePosition>(fixedPositions);
        foreach (var node in nodes.Where(node => !fixedPositions.ContainsKey(node.Id)))
        {
            var position = placed[node.Id];
            while (result.Values.FirstOrDefault(other => Overlap(position, other)) is { } covered)
            {
                position = position with { X = covered.X + CardWidth + ColumnGap };
            }

            result[node.Id] = position;
        }

        return result;
    }
}
