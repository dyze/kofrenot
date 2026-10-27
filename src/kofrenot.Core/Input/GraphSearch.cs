using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// Finds the nodes while typing: a name matches when one of its words starts with the typed text
/// ("her" finds "Frank Herbert"), ignoring case, accents and extra spaces; then the nodes whose
/// field values match the same way, all offline.
/// </summary>
public static class GraphSearch
{
    public const int DefaultLimit = 20;

    /// <summary>
    /// The matching nodes: exact names first, then names starting with the text, then the others,
    /// by name; then the nodes whose values match, by name, with the first field matching.
    /// </summary>
    public static IReadOnlyList<SearchHit> Find(KnowledgeGraph graph, string text, int limit = DefaultLimit)
    {
        var query = NameKey.Of(text);
        if (query.Length == 0)
        {
            return [];
        }

        var byName = graph.NodesByKey
            .Select(entry => (Key: entry.Key, Nodes: entry.Value, Position: WordStartOf(entry.Key, query)))
            .Where(entry => entry.Position >= 0)
            .OrderBy(entry => entry.Key == query ? 0 : entry.Position == 0 ? 1 : 2)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Nodes.Select((node, index) => new SearchHit(node, index + 1, entry.Nodes.Count, entry.Key == query)))
            .Take(limit)
            .ToList();
        return byName.Count < limit ? [.. byName, .. ByValue(graph, query, byName.Select(hit => hit.Node.Id).ToHashSet()).Take(limit - byName.Count)] : byName;
    }

    // The nodes not found by name whose value of a field matches, by name, each with its first field matching.
    private static IEnumerable<SearchHit> ByValue(KnowledgeGraph graph, string query, HashSet<Guid> found) =>
        graph.NodesWithFields
            .Where(node => !found.Contains(node.Id))
            .Select(node => (Node: node, Field: NodeFields.Of(graph, node.Id).FirstOrDefault(field => field.Value is not null && WordStartOf(NameKey.Of(field.Display), query) >= 0)))
            .Where(match => match.Field is not null)
            .OrderBy(match => NameKey.Of(match.Node.Name), StringComparer.Ordinal)
            .ThenBy(match => match.Node.Created)
            .Select(match =>
            {
                var homonyms = graph.NodesNamed(match.Node.Name);
                var rank = homonyms.TakeWhile(other => other.Id != match.Node.Id).Count() + 1;
                return new SearchHit(match.Node, rank, homonyms.Count, IsExact: false, $"{match.Field!.Label}: {match.Field.Display}");
            });

    /// <summary>Whether a name matches the typed text, as in <see cref="Find"/>; an empty text matches nothing.</summary>
    public static Func<string, bool> MatcherOf(string text)
    {
        var query = NameKey.Of(text);
        return name => query.Length > 0 && WordStartOf(NameKey.Of(name), query) >= 0;
    }

    // Where the query starts a word of the key, or -1.
    private static int WordStartOf(string key, string query)
    {
        for (var index = key.IndexOf(query, StringComparison.Ordinal); index >= 0; index = key.IndexOf(query, index + 1, StringComparison.Ordinal))
        {
            if (index == 0 || key[index - 1] == ' ')
            {
                return index;
            }
        }

        return -1;
    }
}
