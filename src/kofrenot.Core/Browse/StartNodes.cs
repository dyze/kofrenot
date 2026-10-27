using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Browse;

/// <summary>A recent node, and its latest activity ("changed 5 min ago", "opened yesterday"): null when not known.</summary>
public sealed record RecentNode(Node Node, string? Detail);

/// <summary>
/// What the empty input box (on the phone, the home) lists: the pinned nodes, then the recent ones,
/// each node once. A recent node was opened on this device (<see cref="RecentNodes"/>), or changed on
/// any device in the last <see cref="ChangedWithin"/>: newest first by the later of both, at most
/// <see cref="MaxRecent"/>. Changes are read from the vault, which every device has: nothing more is stored or synced.
/// </summary>
public sealed record StartNodes(IReadOnlyList<Node> Pinned, IReadOnlyList<RecentNode> Recent)
{
    public const int MaxRecent = 10;

    public static readonly TimeSpan ChangedWithin = TimeSpan.FromDays(7);

    public static StartNodes Of(KnowledgeGraph graph, IReadOnlyList<OpenedNode> opened, DateTimeOffset now)
    {
        var pinned = graph.PinnedNodes;
        var pinnedIds = pinned.Select(node => node.Id).ToHashSet();
        var latest = new Dictionary<Guid, Activity>();
        AddOpened(graph, opened, pinnedIds, latest);
        AddChanged(graph, now, pinnedIds, latest);
        var recent = latest.Values
            .OrderByDescending(activity => activity.At.HasValue)
            .ThenByDescending(activity => activity.At)
            .ThenBy(activity => activity.Order)
            .Take(MaxRecent)
            .Select(activity => new RecentNode(activity.Node, activity.Detail(now)))
            .ToList();
        return new StartNodes(pinned, recent);
    }

    // Those opened before their time was kept come after the others, in their order.
    private static void AddOpened(KnowledgeGraph graph, IReadOnlyList<OpenedNode> opened, HashSet<Guid> pinned, Dictionary<Guid, Activity> latest)
    {
        for (var order = 0; order < opened.Count; order++)
        {
            if (graph.FindNode(opened[order].Node) is { } node && !pinned.Contains(node.Id))
            {
                latest.TryAdd(node.Id, new Activity(node, opened[order].At, Changed: false, order));
            }
        }
    }

    private static void AddChanged(KnowledgeGraph graph, DateTimeOffset now, HashSet<Guid> pinned, Dictionary<Guid, Activity> latest)
    {
        foreach (var node in graph.Nodes.Where(node => !pinned.Contains(node.Id)))
        {
            var changed = NodeFacts.ModifiedOf(graph, node);
            var openedLater = latest.TryGetValue(node.Id, out var seen) && seen.At > changed;
            if (now - changed <= ChangedWithin && !openedLater)
            {
                latest[node.Id] = new Activity(node, changed, Changed: true, Order: 0);
            }
        }
    }

    private sealed record Activity(Node Node, DateTimeOffset? At, bool Changed, int Order)
    {
        public string? Detail(DateTimeOffset now) =>
            At is { } at ? $"{(Changed ? "changed" : "opened")} {TimeAgo.Of(at, now)}" : null;
    }
}
