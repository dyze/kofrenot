using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;

namespace Kofrenot.UI.Views;

/// <summary>A mapping chosen in <see cref="MappingDialog"/>: the mapping node's name and its definition.</summary>
public sealed record MappingEdit(string Name, MappingDefinition Definition);

/// <summary>A field of the mapping's type, as edited: the key read for it (empty: not imported), and whether it holds another name of the node.</summary>
public sealed class FieldRuleRow(FieldDefinition field, string key, bool isOtherName, IReadOnlyList<string> keys)
{
    public FieldDefinition Field { get; } = field;

    public string Label => Field.Name;

    public string Key { get; set; } = key;

    public bool IsOtherName { get; set; } = isOtherName;

    /// <summary>The keys of the sample file, offered.</summary>
    public IReadOnlyList<string> Keys { get; } = keys;
}

/// <summary>A link rule, as edited: the key whose values name the linked nodes, their type, and the link read from the imported node.</summary>
public sealed class LinkRuleRow(string key, NodeType? nodeType, LinkVerb? verb, IReadOnlyList<string> keys, IReadOnlyList<NodeType> nodeTypes, IReadOnlyList<LinkVerb> verbs)
{
    public string Key { get; set; } = key;

    public NodeType? NodeType { get; set; } = nodeType;

    public LinkVerb? Verb { get; set; } = verb;

    public IReadOnlyList<string> Keys { get; } = keys;

    public IReadOnlyList<NodeType> NodeTypes { get; } = nodeTypes;

    public IReadOnlyList<LinkVerb> Verbs { get; } = verbs;

    /// <summary>The rule, or null while incomplete.</summary>
    public LinkRule? ToRule() =>
        Key.Trim() is { Length: > 0 } trimmed && NodeType is { } type && Verb is { } verb ? new LinkRule(trimmed, type.Id, verb.Type.Id, verb.IsReverse) : null;
}
