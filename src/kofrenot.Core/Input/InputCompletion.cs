using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// The proposal shown in grey after the text of the input box: the rest of a node's name starting
/// with the side typed, the shortest first; or else, on the first side, the rest of a link type
/// starting with its last words. Case is ignored; a complete link type gets no proposal.
/// </summary>
public static class InputCompletion
{
    /// <param name="typed">The side typed: the whole text, or what follows the link type once it is a label.</param>
    /// <param name="linkTypeTyped">The link type is a label already: only a node is proposed.</param>
    public static Proposal? Propose(KnowledgeGraph graph, string typed, bool linkTypeTyped)
    {
        typed = typed.TrimStart();
        if (typed.Length == 0)
        {
            return null;
        }

        return NodeProposal(graph, typed) ?? (linkTypeTyped ? null : LinkTypeProposal(graph.Types, typed));
    }

    private static Proposal? NodeProposal(KnowledgeGraph graph, string typed) =>
        graph.NodesByKey.Values
            .Where(nodes => nodes.Count > 0)
            .Select(nodes => nodes[0].Name)
            .Where(name => name.Length > typed.Length && name.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name.Length)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => new Proposal(name[typed.Length..], name, IsLinkType: false))
            .FirstOrDefault();

    // The longest run of last words that starts a link type, after at least one word.
    private static Proposal? LinkTypeProposal(TypeCatalog types, string typed)
    {
        var words = InputParser.Words(typed);
        var phrases = types.Verbs.SelectMany(verb => verb.Phrases).ToList();
        var space = char.IsWhiteSpace(typed[^1]) ? " " : string.Empty;
        for (var start = 1; start < words.Length; start++)
        {
            var phrase = string.Join(' ', words[start..]) + space;
            if (phrases.Any(candidate => candidate.Equals(phrase, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            var match = phrases
                .Where(candidate => candidate.StartsWith(phrase, StringComparison.OrdinalIgnoreCase))
                .OrderBy(candidate => candidate.Length)
                .FirstOrDefault();
            if (match is not null)
            {
                return new Proposal(match[phrase.Length..], match, IsLinkType: true, start);
            }
        }

        return null;
    }
}
