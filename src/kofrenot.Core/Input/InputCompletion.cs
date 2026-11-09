using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// The proposals shown in grey after the text of the input box, the first one first: the rest of each node's name
/// starting with the side typed, the shortest first; then, on the first side, the rest of each link type starting
/// with its last words. Case is ignored; a complete link type proposes only the longer ones of other link types
/// starting with it ("read" proposes "read by"; "is a" not its other phrase "is an").
/// </summary>
public static class InputCompletion
{
    /// <param name="typed">The side typed: the whole text, or what follows the link type once it is a label.</param>
    /// <param name="linkTypeTyped">The link type is a label already: only nodes are proposed.</param>
    public static IEnumerable<Proposal> Proposals(KnowledgeGraph graph, string typed, bool linkTypeTyped)
    {
        typed = typed.TrimStart();
        if (typed.Length == 0)
        {
            return [];
        }

        var nodes = NodeProposals(graph, typed);
        return linkTypeTyped ? nodes : nodes.Concat(LinkTypeProposals(graph.Types, typed));
    }

    /// <summary>
    /// The rest of each link type starting with what is typed after <paramref name="left"/>, a node that stays as it is
    /// (the node of the details): never a node, nor a link type starting inside its name.
    /// </summary>
    public static IEnumerable<Proposal> LinkTypeProposalsAfter(TypeCatalog types, string left, string typed) =>
        typed.Trim().Length == 0 ? [] : LinkTypeProposals(types, $"{left} {typed.TrimStart()}", only: InputParser.Words(left).Length);

    /// <summary>The rest of each node's name starting with the text typed, the shortest first; of those <paramref name="kept"/> only, when given.</summary>
    internal static IEnumerable<Proposal> NodeProposals(KnowledgeGraph graph, string typed, Func<Node, bool>? kept = null) =>
        graph.NodesByKey.Values
            .Select(nodes => nodes.FirstOrDefault(node => kept?.Invoke(node) ?? true))
            .OfType<Node>()
            .Select(node => node.Name)
            .Where(name => name.Length > typed.Length && name.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name.Length)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => new Proposal(name[typed.Length..], name, IsLinkType: false));

    // The link types starting with the longest run of last words that starts one, after at least one word (or after the first only words).
    private static IEnumerable<Proposal> LinkTypeProposals(TypeCatalog types, string typed, int? only = null)
    {
        var words = InputParser.Words(typed);
        var verbs = types.Verbs;
        var space = char.IsWhiteSpace(typed[^1]) ? " " : string.Empty;
        for (var start = only ?? 1; start < words.Length && (only is null || start == only); start++)
        {
            var phrase = string.Join(' ', words[start..]) + space;
            var complete = verbs.Where(verb => verb.Phrases.Contains(phrase, StringComparer.OrdinalIgnoreCase)).ToList();
            var matches = verbs
                .Except(complete)
                .SelectMany(verb => verb.Phrases)
                .Where(candidate => candidate.Length > phrase.Length && candidate.StartsWith(phrase, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(candidate => candidate.Length)
                .Select(match => new Proposal(match[phrase.Length..], match, IsLinkType: true, start))
                .ToList();
            if (matches.Count > 0 || complete.Count > 0)
            {
                return matches;
            }
        }

        return [];
    }
}
