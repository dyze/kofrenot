using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// Finds the nodes while typing: a name matches when one of its words starts with the typed text
/// ("her" finds "Frank Herbert"), ignoring case, accents and extra spaces.
/// </summary>
public static class GraphSearch
{
    public const int DefaultLimit = 20;

    /// <summary>The matching nodes: exact names first, then names starting with the text, then the others, by name.</summary>
    public static IReadOnlyList<SearchHit> Find(KnowledgeGraph graph, string text, int limit = DefaultLimit)
    {
        var query = NameKey.Of(text);
        if (query.Length == 0)
        {
            return [];
        }

        return graph.NodesByKey
            .Select(entry => (Key: entry.Key, Nodes: entry.Value, Position: WordStartOf(entry.Key, query)))
            .Where(entry => entry.Position >= 0)
            .OrderBy(entry => entry.Key == query ? 0 : entry.Position == 0 ? 1 : 2)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Nodes.Select((node, index) => new SearchHit(node, index + 1, entry.Nodes.Count, entry.Key == query)))
            .Take(limit)
            .ToList();
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
