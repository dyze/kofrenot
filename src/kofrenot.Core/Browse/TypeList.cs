using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Browse;

/// <summary>A node or link type as listed to read: its name, what it holds, and whether kofrenot gives it.</summary>
public sealed record TypeLine(string Name, string Detail, bool IsBuiltIn);

/// <summary>
/// The vault's node types and link types, to read where they are not changed (the phone): built-in ones first,
/// then the user's by name. A node type with its fields; a link type with its reverse, aliases and qualifiers.
/// </summary>
public static class TypeList
{
    public static IReadOnlyList<TypeLine> NodeTypes(KnowledgeGraph graph) =>
        Sorted(graph.Types.Chosen.Select(type => new TypeLine(
            type.Name,
            FieldsText(type.Fields.Where(field => !field.Kind.IsContent()).ToList(), "fields"),
            TypeCatalog.BuiltIn.FindNodeType(type.Id) is not null)));

    public static IReadOnlyList<TypeLine> LinkTypes(KnowledgeGraph graph) =>
        Sorted(graph.Types.LinkTypes.Select(type => new TypeLine(
            type.Name,
            string.Join(" · ", LinkParts(type, graph.LinkTypes.FieldsOf(type.Id))),
            TypeCatalog.BuiltIn.FindLinkType(type.Id) is not null)));

    private static IReadOnlyList<TypeLine> Sorted(IEnumerable<TypeLine> lines) =>
        [.. lines.OrderByDescending(line => line.IsBuiltIn).ThenBy(line => line.Name, StringComparer.CurrentCultureIgnoreCase)];

    private static IEnumerable<string> LinkParts(LinkType type, IReadOnlyList<FieldDefinition> qualifiers)
    {
        yield return type.IsSymmetric ? "the same both ways" : $"reverse: {type.ReversePhrases![0]}";
        var aliases = type.Phrases.Skip(1).Concat(type.ReversePhrases?.Skip(1) ?? []).ToList();
        if (aliases.Count > 0)
        {
            yield return $"aliases: {string.Join(", ", aliases)}";
        }

        if (qualifiers.Count > 0)
        {
            yield return FieldsText(qualifiers, "qualifiers");
        }
    }

    private static string FieldsText(IReadOnlyList<FieldDefinition> fields, string what) =>
        fields.Count == 0
            ? $"No {what}"
            : $"{char.ToUpperInvariant(what[0])}{what[1..]}: {string.Join(", ", fields.Select(field => $"{field.Name} ({FieldKinds.NameOf(field.Kind)})"))}";
}
