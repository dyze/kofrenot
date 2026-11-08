using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>A node a query found; by a time, with when (<c>changed 3 days ago</c>).</summary>
public sealed record QueryHit(Node Node, string? Detail);

/// <summary>
/// The nodes a query found, the first <see cref="NodeQuerySearch.Limit"/> of them (<see cref="Total"/> in all), or the
/// <see cref="Problem"/> that stops it; <see cref="Summary"/> says it under the input box.
/// </summary>
public sealed record QueryResult(IReadOnlyList<QueryHit> Hits, int Total, string? Problem = null)
{
    public string Summary => Problem ?? Total switch
    {
        0 => "No node found.",
        1 => "1 node found. Down, then Enter to see its details.",
        _ when Total > Hits.Count => $"{Total} nodes found, the first {Hits.Count} listed. Up/Down, then Enter to see the details of one.",
        _ => $"{Total} nodes found. Up/Down, then Enter to see the details of one.",
    };
}

/// <summary>
/// Runs a <see cref="NodeQuery"/> on the graph, offline: by time, the newest first (those opened before their time was kept
/// last); else by name. A day is the local one of <c>zone</c>; a node changes when <see cref="NodeFacts.ModifiedOf"/> says.
/// </summary>
public static class NodeQuerySearch
{
    public const int Limit = 200;

    /// <param name="limit">How many hits are kept, the others only counted: <see cref="Limit"/> for the input box, all to import them.</param>
    public static QueryResult Find(KnowledgeGraph graph, NodeQuery query, IReadOnlyList<OpenedNode> opened, DateTimeOffset now, TimeZoneInfo zone, int limit = Limit)
    {
        var nodes = query.Link is { } link ? Linked(graph, link) : graph.Nodes;
        if (query.Type is { } type)
        {
            nodes = nodes.Where(node => graph.HasType(node.Id, type.Id));
        }

        if (query.Name.Length > 0)
        {
            var matches = GraphSearch.MatcherOf(query.Name);
            nodes = nodes.Where(node => matches(node.Name));
        }

        if (query.Time is not { } time)
        {
            return ResultOf(limit, nodes.OrderBy(node => NameKey.Of(node.Name), StringComparer.Ordinal).ThenBy(node => node.Created).Select(node => new QueryHit(node, null)));
        }

        if (time.DaysAt(DayOf(now, zone)) is not { } days)
        {
            return new QueryResult([], 0, $"“{time.Period.Trim()}” is no period. {TimeQuery.Hint}");
        }

        var at = TimesOf(graph, time.Kind, opened);
        if (time.Kind == TimeKind.Opened)
        {
            var openedHere = opened.Select(entry => entry.Node).ToHashSet();
            nodes = nodes.Where(node => openedHere.Contains(node.Id));
        }

        // Opened before its time was kept: listed for any time only.
        return ResultOf(limit, nodes
            .Select(node => (Node: node, At: at(node)))
            .Where(found => found.At is { } when ? InDays(DayOf(when, zone), days) : days is (null, null))
            .OrderByDescending(found => found.At.HasValue)
            .ThenByDescending(found => found.At)
            .ThenBy(found => NameKey.Of(found.Node.Name), StringComparer.Ordinal)
            .Select(found => new QueryHit(found.Node, found.At is { } when ? $"{time.Word} {TimeAgo.Of(when, now)}" : null)));
    }

    private static QueryResult ResultOf(int limit, IEnumerable<QueryHit> hits)
    {
        var all = hits.ToList();
        return new QueryResult(all.Take(limit).ToList(), all.Count);
    }

    // The nodes reading the link's verb to the node typed: those named so, else those whose names match; none typed, any.
    private static IEnumerable<Node> Linked(KnowledgeGraph graph, LinkQuery query)
    {
        bool Reads(Link link, Guid from) =>
            graph.Types.VerbOf(link, from) is { } verb && verb.Type.Id == query.Verb.Type.Id && verb.IsReverse == query.Verb.IsReverse;

        if (query.Node.Length == 0)
        {
            return graph.Links
                .SelectMany(link => new[] { link.From, link.To }.Where(end => Reads(link, end)))
                .Distinct()
                .Select(graph.FindNode)
                .OfType<Node>();
        }

        var matches = GraphSearch.MatcherOf(query.Node);
        var targets = graph.NodesNamed(query.Node) is { Count: > 0 } named ? named : graph.Nodes.Where(node => matches(node.Name)).ToList();
        return targets
            .SelectMany(target => graph.LinksOf(target.Id)
                .Select(link => (Link: link, Other: link.Other(target.Id)))
                .Where(end => end.Other != target.Id && Reads(end.Link, end.Other))
                .Select(end => end.Other))
            .Distinct()
            .Select(graph.FindNode)
            .OfType<Node>();
    }

    private static Func<Node, DateTimeOffset?> TimesOf(KnowledgeGraph graph, TimeKind kind, IReadOnlyList<OpenedNode> opened)
    {
        var openedAt = opened.DistinctBy(entry => entry.Node).ToDictionary(entry => entry.Node, entry => entry.At);
        return kind switch
        {
            TimeKind.Changed => node => NodeFacts.ModifiedOf(graph, node),
            TimeKind.Added => node => node.Created,
            _ => node => openedAt.GetValueOrDefault(node.Id),
        };
    }

    private static DateOnly DayOf(DateTimeOffset at, TimeZoneInfo zone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);

    private static bool InDays(DateOnly day, (DateOnly? From, DateOnly? To) days) =>
        (days.From is not { } from || day >= from) && (days.To is not { } to || day <= to);
}
