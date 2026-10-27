using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Browse;

/// <summary>A node changed lately, and when it last changed (<see cref="NodeFacts.Modified"/>).</summary>
public sealed record ChangedNode(Node Node, DateTimeOffset At);

/// <summary>
/// The nodes changed lately in a vault, on any device, newest first: what the empty input box
/// lists, so a change made on one device is at hand on the others. Read from the vault's changes,
/// which every device has: nothing more is stored or synced.
/// </summary>
public static class ChangedLately
{
    public const int Max = 10;

    public static readonly TimeSpan Within = TimeSpan.FromDays(7);

    /// <summary>At most <see cref="Max"/> nodes changed in the <see cref="Within"/> before <paramref name="now"/>, newest first.</summary>
    public static IReadOnlyList<ChangedNode> Of(KnowledgeGraph graph, DateTimeOffset now) =>
        graph.Nodes
            .Select(node => new ChangedNode(node, NodeFacts.Of(graph, node).Modified))
            .Where(changed => now - changed.At <= Within)
            .OrderByDescending(changed => changed.At)
            .Take(Max)
            .ToList();
}
