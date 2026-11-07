using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>A way to start a query (<see cref="QueryCompletion.Starts"/>): the text it puts in the box, and what it lists.</summary>
public sealed record QueryStart(string Insert, string Detail);

/// <summary>
/// What a query (<see cref="QueryParser"/>) is proposed in grey, as names are (<see cref="InputCompletion"/>): its first word
/// (a type's name and <c>:</c>, <c>changed:</c>, <c>added:</c>, <c>opened:</c>, a link phrase), then a period after a time,
/// a node's name after a link phrase, a node of the type after a type. Each adds text, never a label.
/// </summary>
public static class QueryCompletion
{
    public const string Hint = "A query: a type (?person:), a link (?contained by Kitchen) or a time (?changed: this week). Up/Down, then Tab, or a click, to start one.";

    private static readonly string[] Periods = ["today", "yesterday", "this week", "this month", "this year"];

    private static readonly (string Word, string Detail)[] Times =
    [
        ("changed", "the nodes by their last change"),
        ("added", "the nodes by when they were added"),
        ("opened", "the nodes by when you last opened them here"),
    ];

    /// <summary>The ways to start one, listed for <c>?</c> alone: each type (with its nodes' number), each time, each link phrase.</summary>
    public static IReadOnlyList<QueryStart> Starts(KnowledgeGraph graph)
    {
        var types = graph.Types.NodeTypes.Select(type =>
        {
            var count = graph.Nodes.Count(node => graph.HasType(node.Id, type.Id));
            return new QueryStart($"{QueryParser.Mark}{type.Name}: ", count == 1 ? "1 node" : $"{count} nodes");
        });
        var times = Times.Select(time => new QueryStart($"{QueryParser.Mark}{time.Word}: ", time.Detail));
        var links = graph.Types.Verbs.Select(verb => new QueryStart($"{QueryParser.Mark}{verb.Name} ", "the nodes linked so to a node"));
        return [.. types, .. times, .. links];
    }

    /// <summary>What Tab may add to a query, the first one first; none when nothing fits.</summary>
    public static IEnumerable<Proposal> Proposals(KnowledgeGraph graph, string text)
    {
        var rest = QueryParser.AfterMark(text).TrimStart();
        if (TimePeriod(rest) is { } period)
        {
            return Of(Periods, period, emptyToo: rest.Length > 0 && char.IsWhiteSpace(rest[^1]));
        }

        NodeType? type = null;
        if (QueryParser.TypeOf(rest, graph.Types) is { } typed)
        {
            type = typed.Type;
            rest = rest[(rest.IndexOf(':') + 1)..].TrimStart();
            if (TimePeriod(rest) is { } typedPeriod)
            {
                return Of(Periods, typedPeriod, emptyToo: rest.Length > 0 && char.IsWhiteSpace(rest[^1]));
            }
        }

        if (QueryParser.LinkOf(rest, graph.Types) is { } link)
        {
            var node = link.Node.Length > 0 && char.IsWhiteSpace(rest[^1]) ? $"{link.Node} " : link.Node;
            return node.Length == 0 ? [] : AsQuery(InputCompletion.NodeProposals(graph, node));
        }

        if (rest.Length == 0)
        {
            return [];
        }

        var words = (type is null ? graph.Types.NodeTypes.Select(nodeType => $"{nodeType.Name}: ") : [])
            .Concat(Times.Select(time => $"{time.Word}: "))
            .Concat(graph.Types.Verbs.SelectMany(verb => verb.Phrases).Select(phrase => $"{phrase} "));
        var nodes = type is { } nodeType ? AsQuery(InputCompletion.NodeProposals(graph, rest, node => graph.HasType(node.Id, nodeType.Id))) : [];
        return Of(words, rest, emptyToo: false).Concat(nodes);
    }

    // After "changed:": the period typed so far, untrimmed at its end; null when the text is no time.
    private static string? TimePeriod(string text) =>
        text.IndexOf(':') is var colon and >= 0 && TimeQuery.KindOf(text[..colon]) is not null ? text[(colon + 1)..].TrimStart() : null;

    // The words starting with the text typed, that add more than spaces, the shortest first.
    private static IEnumerable<Proposal> Of(IEnumerable<string> words, string typed, bool emptyToo) =>
        typed.Length == 0 && !emptyToo
            ? []
            : words
                .Where(word => word.StartsWith(typed, StringComparison.OrdinalIgnoreCase) && word[typed.Length..].Trim().Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(word => word.Length)
                .ThenBy(word => word, StringComparer.OrdinalIgnoreCase)
                .Select(word => new Proposal(word[typed.Length..], word, IsLinkType: false, IsQueryText: true));

    private static IEnumerable<Proposal> AsQuery(IEnumerable<Proposal> proposals) => proposals.Select(proposal => proposal with { IsQueryText = true });
}
