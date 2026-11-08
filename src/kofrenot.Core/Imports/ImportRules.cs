using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Imports;

/// <summary>A field rule whose field the type still has.</summary>
internal sealed record ResolvedField(string Key, FieldDefinition Field);

/// <summary>A link rule whose link type and node type are still known: <see cref="Verb"/> reads it from the imported node.</summary>
internal sealed record ResolvedLink(LinkRule Rule, string Verb);

/// <summary>
/// A mapping's rules against the vault as it is now: those whose field or type is gone are left
/// out, each named in <see cref="Warnings"/>. A field rule only writes a small field (not an image).
/// </summary>
internal sealed record ImportRules(
    NodeType Type,
    IReadOnlyList<string> NameKeys,
    FieldDefinition? Match,
    IReadOnlyList<FieldDefinition> OtherNames,
    IReadOnlyList<ResolvedField> Fields,
    IReadOnlyList<ResolvedLink> Links,
    IReadOnlyList<string> Warnings)
{
    /// <summary>The rules, or null when the mapping's node type is gone.</summary>
    public static ImportRules? Of(KnowledgeGraph graph, MappingDefinition mapping)
    {
        var types = graph.Types;
        if (types.FindNodeType(mapping.NodeType) is not { } type)
        {
            return null;
        }

        var warnings = new List<string>();
        var fields = new List<ResolvedField>();
        foreach (var rule in mapping.Fields)
        {
            if (type.Fields.FirstOrDefault(field => field.Id == rule.Field) is { } field && field.Kind.IsSmall())
            {
                fields.Add(new ResolvedField(rule.Key, field));
            }
            else
            {
                warnings.Add($"“{rule.Key}” is not imported: its field is no longer a field of {type.Name} kofrenot can write.");
            }
        }

        var links = new List<ResolvedLink>();
        foreach (var rule in mapping.Links)
        {
            if (types.FindLinkType(rule.LinkType) is { } linkType && types.FindNodeType(rule.NodeType) is not null)
            {
                links.Add(new ResolvedLink(rule, new LinkVerb(linkType, IsReverse: rule.IsReverse && !linkType.IsSymmetric).Name));
            }
            else
            {
                warnings.Add($"“{rule.Key}” is not linked: its link type or node type is gone.");
            }
        }

        var match = mapping.Match is { } id ? Written(id) : null;
        if (mapping.Match is not null && match is null)
        {
            warnings.Add($"Nodes are found by their names only: the match field is no longer imported.");
        }

        return new ImportRules(type, mapping.NameKeys, match, [.. mapping.OtherNames.Select(Written).OfType<FieldDefinition>()], fields, links, warnings);

        // A field the rules write: one to compare with.
        FieldDefinition? Written(string field) => fields.FirstOrDefault(rule => rule.Field.Id == field)?.Field;
    }
}
