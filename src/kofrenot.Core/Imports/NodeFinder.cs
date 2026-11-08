using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Imports;

/// <summary>How a record found its node.</summary>
internal enum FoundKind
{
    New,
    Found,

    /// <summary>Found by a name, its match value another: the user chooses.</summary>
    ToCheck,

    Ambiguous,
}

/// <summary>A record's node: <see cref="By"/> names what found it when not the node's name (<i>Original title “The Dispossessed”</i>).</summary>
internal sealed record Found(FoundKind Kind, Node? Node = null, string? By = null, string? Reason = null);

/// <summary>
/// Finds a record's node among the nodes of the mapping's type: by its match value (an ISBN) first;
/// else by its names (its name and other names), compared with each node's name and other-name
/// fields (case and accents ignored, <see cref="NameKey"/>). Several nodes found: ambiguous.
/// </summary>
internal sealed class NodeFinder
{
    private readonly KnowledgeGraph _graph;
    private readonly ImportRules _rules;
    private readonly Dictionary<string, List<Node>> _byMatch = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<(Node Node, FieldDefinition? Field, string Name)>> _byName = new(StringComparer.Ordinal);

    public NodeFinder(KnowledgeGraph graph, ImportRules rules)
    {
        _graph = graph;
        _rules = rules;
        foreach (var node in graph.Nodes.Where(node => graph.HasType(node.Id, rules.Type.Id)))
        {
            if (rules.Match is { } match && ValueOf(node, match) is { } value)
            {
                Add(_byMatch, value, node);
            }

            Add(_byName, NameKey.Of(node.Name), (node, null, node.Name));
            foreach (var field in rules.OtherNames)
            {
                if (ValueOf(node, field) is { } other)
                {
                    Add(_byName, NameKey.Of(other), (node, field, other));
                }
            }
        }
    }

    /// <summary>A node's value of a field of the mapping's type; null when it has none.</summary>
    public string? ValueOf(Node node, FieldDefinition field) =>
        _graph.FieldsOf(node.Id).FirstOrDefault(values => values.Owner == _rules.Type.Id && values.Field == field.Id)?.Shown.Value;

    /// <summary>
    /// The node of a record of <paramref name="matchValue"/> (none when it has none), named
    /// <paramref name="name"/>, whose other names are <paramref name="otherNames"/> (from its other-name fields).
    /// </summary>
    public Found Find(string? matchValue, string name, IReadOnlyList<(FieldDefinition Field, string Value)> otherNames)
    {
        if (matchValue is not null && _byMatch.TryGetValue(matchValue, out var matched))
        {
            return matched is [var node]
                ? new Found(FoundKind.Found, node)
                : new Found(FoundKind.Ambiguous, Reason: $"{matched.Count} nodes have this {_rules.Match!.Name}");
        }

        var names = otherNames.Select(other => ((FieldDefinition?)other.Field, other.Value)).Prepend((null, name));
        var candidates = names
            .SelectMany(recordName => (_byName.GetValueOrDefault(NameKey.Of(recordName.Value)) ?? []).Select(nodeName => (Record: recordName.Item1, Node: nodeName)))
            .ToList();
        var nodes = candidates.Select(candidate => candidate.Node.Node).Distinct().ToList();
        if (nodes.Count == 0)
        {
            return new Found(FoundKind.New);
        }

        if (nodes.Count > 1)
        {
            return new Found(FoundKind.Ambiguous, Reason: $"{nodes.Count} nodes have this name");
        }

        // Found by name when a name matched a name; else by the other-name field that matched, either side.
        var found = nodes[0];
        var byText = candidates.Any(candidate => candidate.Record is null && candidate.Node.Field is null) ? null
            : candidates.Select(candidate => $"{(candidate.Node.Field ?? candidate.Record)!.Name} “{candidate.Node.Name}”").First();
        var isOther = matchValue is not null && _rules.Match is { } match && ValueOf(found, match) is { } other && other != matchValue;
        return new Found(isOther ? FoundKind.ToCheck : FoundKind.Found, found, byText);
    }

    private static void Add<T>(Dictionary<string, List<T>> index, string key, T item)
    {
        if (!index.TryGetValue(key, out var list))
        {
            index[key] = list = [];
        }

        list.Add(item);
    }
}
