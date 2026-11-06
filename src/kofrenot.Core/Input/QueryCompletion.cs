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

    /// <summary>What Tab adds to a query; null when nothing is proposed.</summary>
    public static Proposal? Propose(KnowledgeGraph graph, string text)
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
            return node.Length == 0 ? null : AsQuery(InputCompletion.NodeProposal(graph, node));
        }

        if (rest.Length == 0)
        {
            return null;
        }

        var words = (type is null ? graph.Types.NodeTypes.Select(nodeType => $"{nodeType.Name}: ") : [])
            .Concat(Times.Select(time => $"{time.Word}: "))
            .Concat(graph.Types.Verbs.SelectMany(verb => verb.Phrases).Select(phrase => $"{phrase} "));
        return Of(words, rest, emptyToo: false)
            ?? (type is { } nodeType ? AsQuery(InputCompletion.NodeProposal(graph, rest, node => graph.HasType(node.Id, nodeType.Id))) : null);
    }

    // After "changed:": the period typed so far, untrimmed at its end; null when the text is no time.
    private static string? TimePeriod(string text) =>
        text.IndexOf(':') is var colon and >= 0 && TimeQuery.KindOf(text[..colon]) is not null ? text[(colon + 1)..].TrimStart() : null;

    // The shortest word starting with the text typed, that adds more than spaces.
    private static Proposal? Of(IEnumerable<string> words, string typed, bool emptyToo) =>
        typed.Length == 0 && !emptyToo
            ? null
            : words
                .Where(word => word.StartsWith(typed, StringComparison.OrdinalIgnoreCase) && word[typed.Length..].Trim().Length > 0)
                .OrderBy(word => word.Length)
                .ThenBy(word => word, StringComparer.OrdinalIgnoreCase)
                .Select(word => new Proposal(word[typed.Length..], word, IsLinkType: false, IsQueryText: true))
                .FirstOrDefault();

    private static Proposal? AsQuery(Proposal? proposal) => proposal is null ? null : proposal with { IsQueryText = true };
}
