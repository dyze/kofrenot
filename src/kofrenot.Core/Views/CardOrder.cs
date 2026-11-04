using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Views;

/// <summary>
/// The order of a list view's cards: the pinned ones first, then the others; in each part, the cards
/// arranged by hand (<see cref="Move"/>), then the rest as given (by name). A card's place is kept as
/// its rank in the view's placements (<see cref="NodesPlaced"/>, as <see cref="NodePosition.Y"/>), so it
/// syncs as a graph view's positions do: for each card the newest rank wins, with no conflict.
/// </summary>
public sealed class CardOrder(KnowledgeGraph graph, Guid view, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary><paramref name="nodes"/> in the order of their cards.</summary>
    public IReadOnlyList<Node> Of(IEnumerable<Node> nodes)
    {
        var ranks = graph.PositionsIn(view);
        return nodes
            .Select((node, given) => (Node: node, Given: given, Rank: ranks.TryGetValue(node.Id, out var position) ? position.Y : (double?)null))
            .OrderBy(card => !IsPinned(card.Node))
            .ThenBy(card => card.Rank is null)
            .ThenBy(card => card.Rank)
            .ThenBy(card => card.Given)
            .Select(card => card.Node)
            .ToList();
    }

    public bool IsPinned(Node node) => graph.PinsOf(node.Id).Count > 0;

    /// <summary>
    /// The placement putting the card at <paramref name="from"/> in <paramref name="cards"/> (in the order
    /// shown) at <paramref name="to"/>, every card then ranked by its place; null when it stays, or would
    /// leave its part (pinned or not).
    /// </summary>
    public NodesPlaced? Move(IReadOnlyList<Node> cards, int from, int to)
    {
        if (from == to || IsPinned(cards[from]) != IsPinned(cards[to]))
        {
            return null;
        }

        var moved = cards.ToList();
        var card = moved[from];
        moved.RemoveAt(from);
        moved.Insert(to, card);
        var kept = graph.PositionsIn(view);
        var positions = moved
            .Select((node, rank) => new NodePosition(node.Id, 0, rank))
            .Where(position => !kept.TryGetValue(position.Node, out var current) || current != position)
            .ToList();
        return new NodesPlaced(Guid.NewGuid(), _clock.GetUtcNow(), view, positions);
    }
}
