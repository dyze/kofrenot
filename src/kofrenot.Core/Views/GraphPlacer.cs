using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Views;

/// <summary>
/// Turns cards moved by hand in a graph view into the change that keeps them there, and a reset of
/// the layout into the removal of every placement of the view, so the layout places its nodes again.
/// </summary>
public sealed class GraphPlacer(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>
    /// The placement keeping <paramref name="moved"/> where they were dropped, rounded to whole units;
    /// null when none moved from where it was kept.
    /// </summary>
    public NodesPlaced? Move(Guid view, IEnumerable<NodePosition> moved)
    {
        var kept = graph.PositionsIn(view);
        var positions = moved
            .Select(position => position with { X = Math.Round(position.X), Y = Math.Round(position.Y) })
            .Where(position => !kept.TryGetValue(position.Node, out var current) || current != position)
            .ToList();
        return positions.Count == 0 ? null : new NodesPlaced(Guid.NewGuid(), _clock.GetUtcNow(), view, positions);
    }

    /// <summary>How many nodes of the view were moved by hand: what a reset gives back to the layout.</summary>
    public int MovedCount(Guid view) => graph.PositionsIn(view).Count;

    /// <summary>The removal of every placement of the view; null when no node was moved.</summary>
    public ChangesRemoved? Reset(Guid view) => graph.PlacementsOf(view) is { Count: > 0 } placements
        ? new ChangesRemoved(Guid.NewGuid(), _clock.GetUtcNow(), placements.Select(placed => placed.Id).ToList())
        : null;
}
