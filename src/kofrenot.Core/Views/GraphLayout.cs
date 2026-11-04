using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Views;

/// <summary>The rectangle cards cover, their whole size included, in the view's units.</summary>
public sealed record GraphBounds(double Left, double Top, double Right, double Bottom)
{
    public double Width => Right - Left;

    public double Height => Bottom - Top;
}

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

    /// <summary>What the cards cover; null when there is none.</summary>
    public static GraphBounds? BoundsOf(IEnumerable<NodePosition> cards)
    {
        var all = cards.ToList();
        return all.Count == 0 ? null : new GraphBounds(
            all.Min(card => card.X) - (CardWidth / 2),
            all.Min(card => card.Y) - (CardHeight / 2),
            all.Max(card => card.X) + (CardWidth / 2),
            all.Max(card => card.Y) + (CardHeight / 2));
    }

    /// <summary>Where the line from a card's centre towards a point leaves the card: an arrow ends there. The centre for a point inside.</summary>
    public static (double X, double Y) BorderPoint(NodePosition card, double towardsX, double towardsY)
    {
        var (dx, dy) = (towardsX - card.X, towardsY - card.Y);
        var scale = Math.Max(Math.Abs(dx) / (CardWidth / 2), Math.Abs(dy) / (CardHeight / 2));
        return scale <= 1 ? (card.X, card.Y) : (card.X + (dx / scale), card.Y + (dy / scale));
    }

    /// <summary>The point of a card's border closest to a point, inside the card or out: where its link handle goes.</summary>
    public static (double X, double Y) NearestBorderPoint(NodePosition card, double x, double y)
    {
        var (halfWidth, halfHeight) = (CardWidth / 2, CardHeight / 2);
        var (dx, dy) = (Math.Clamp(x - card.X, -halfWidth, halfWidth), Math.Clamp(y - card.Y, -halfHeight, halfHeight));
        // Inside, the nearest side wins; outside, the clamped point is on the border already.
        if (Math.Abs(dx) < halfWidth && Math.Abs(dy) < halfHeight)
        {
            if (halfWidth - Math.Abs(dx) < halfHeight - Math.Abs(dy))
            {
                dx = dx < 0 ? -halfWidth : halfWidth;
            }
            else
            {
                dy = dy < 0 ? -halfHeight : halfHeight;
            }
        }

        return (card.X + dx, card.Y + dy);
    }

    /// <summary>The node whose card is at a point, the last placed first (it is drawn over the others); null over none.</summary>
    public static Guid? CardAt(IEnumerable<NodePosition> cards, double x, double y) =>
        cards.LastOrDefault(card => Math.Abs(card.X - x) <= CardWidth / 2 && Math.Abs(card.Y - y) <= CardHeight / 2)?.Node;

    /// <summary>The nodes whose card a box drawn on the board touches, in the order given: what a box selects.</summary>
    public static IReadOnlyList<Guid> CardsIn(IEnumerable<NodePosition> cards, GraphBounds box) =>
        cards
            .Where(card => card.X + (CardWidth / 2) >= box.Left && card.X - (CardWidth / 2) <= box.Right
                && card.Y + (CardHeight / 2) >= box.Top && card.Y - (CardHeight / 2) <= box.Bottom)
            .Select(card => card.Node)
            .ToList();

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
