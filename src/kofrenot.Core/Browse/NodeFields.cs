using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Browse;

/// <summary>
/// A field shown in a node's details, or a qualifier in a link's. <see cref="Owner"/> and <see cref="Field"/>
/// say where its values are written (<see cref="FieldSet"/>). <see cref="TypeName"/> names the node or link
/// type it comes from, or is null for a field of the holder's own, which includes a field that left its type,
/// or a type the node no longer has, keeping its value. <see cref="Label"/> is its name, with where it comes
/// from when another field has that name: <c>Born (Person)</c>, <c>Born (own)</c>.
/// </summary>
/// <param name="Values">Its current values; null before it is set. An image's is its file (<see cref="Image"/>).</param>
public sealed record NodeField(string Owner, string Field, string Name, FieldKind Kind, IReadOnlyList<string> Choices, string? TypeName, string Label, FieldVersions? Values)
{
    public bool IsOwn => TypeName is null;
    /// <summary>The value shown; null when it has none, and for an image.</summary>
    public string? Value => Values?.Shown.Value;

    /// <summary>An image's version shown, its file stored as a dataset version's; null when it has none.</summary>
    public FieldSet? Image => Values?.Shown is { File: not null } shown ? shown : null;

    /// <summary>Whether it has a value, or an image.</summary>
    public bool HasValue => Value is not null || Image is not null;

    /// <summary>The value as shown (<see cref="FieldValue.Display"/>), an image's file name; empty when it has none.</summary>
    public string Display => Values?.Shown is { } shown ? DisplayOf(shown) : string.Empty;

    /// <summary>The values set on two devices before they synced, oldest first, as shown; empty without a conflict.</summary>
    public IReadOnlyList<string> Conflict => Values is { IsConflict: true } values
        ? values.Current.Select(version => DisplayOf(version) is { Length: > 0 } shown ? shown : "(none)").ToList()
        : [];

    private string DisplayOf(FieldSet version) =>
        version.File?.Name ?? (version.Value is { } value ? FieldValue.Display(Kind, value) : string.Empty);

    /// <summary>The ids of its current values: what a new value replaces.</summary>
    public IReadOnlyList<Guid> Current => Values?.Current.Select(version => version.Id).ToList() ?? [];
}

/// <summary>
/// The fields of a node: those of each of its types, in their order, then its own, in the order
/// they were first set. A type's field shows empty until it is set; an own field only shows with a
/// value. Content (a note's text, a dataset's file, a view's definition) is shown apart, not here.
/// A link's qualifiers are listed the same way: its type's, then its own.
/// </summary>
public static class NodeFields
{
    /// <summary>The label of a field of the holder's own whose name another field has.</summary>
    public const string OwnLabel = "own";

    public static IReadOnlyList<NodeField> Of(KnowledgeGraph graph, Guid node) =>
        Of(graph.FieldsOf(node), graph.TypeIdsOf(node).Select(graph.Types.FindNodeType).OfType<NodeType>().Select(type => new TypeFields(type.Id, type.Name, type.Fields)));

    /// <summary>The qualifiers of a link: its type's, then its own, set on it or on a copy.</summary>
    public static IReadOnlyList<NodeField> OfLink(KnowledgeGraph graph, Link link) =>
        Of(graph.QualifiersOf(link), [new TypeFields(link.Type, graph.Types.FindLinkType(link.Type)?.Name ?? link.Type, graph.LinkTypes.FieldsOf(link.Type))]);

    /// <summary>The fields of a node, or the qualifiers of a link: <see cref="Of(KnowledgeGraph, Guid)"/> or <see cref="OfLink"/>.</summary>
    public static IReadOnlyList<NodeField> Of(KnowledgeGraph graph, FieldHolder holder) =>
        holder.IsLink
            ? graph.FindLink(holder.Id) is { } link ? OfLink(graph, link) : []
            : Of(graph, holder.Id);

    /// <summary>The qualifier shown beside a link: the first with a value, its type's first; null when it has none.</summary>
    public static NodeField? ShownOf(KnowledgeGraph graph, Link link) => OfLink(graph, link).FirstOrDefault(field => field.HasValue);

    private static List<NodeField> Of(IReadOnlyList<FieldVersions> set, IEnumerable<TypeFields> types)
    {
        var values = set.ToDictionary(field => (field.Owner, field.Field));
        var fields = new List<(string Owner, FieldDefinition Field, string? TypeName)>();
        foreach (var type in types)
        {
            fields.AddRange(type.Fields.Where(field => !field.Kind.IsContent()).Select(field => (type.Id, field, (string?)type.Name)));
        }

        var ofTypes = fields.Select(field => (field.Owner, field.Field.Id)).ToHashSet();
        fields.AddRange(set
            .Where(field => !ofTypes.Contains((field.Owner, field.Field)) && (field.Shown.Value is not null || field.Shown.File is not null))
            .Select(field => (field.Owner, new FieldDefinition(field.Field, field.Shown.Name, field.Shown.Kind), (string?)null)));

        var sameName = fields.GroupBy(field => NameKey.Of(field.Field.Name)).Where(names => names.Count() > 1).Select(names => names.Key).ToHashSet();
        return fields
            .Select(field => new NodeField(
                field.Owner,
                field.Field.Id,
                field.Field.Name,
                field.Field.Kind,
                field.Field.Choices,
                field.TypeName,
                sameName.Contains(NameKey.Of(field.Field.Name)) ? $"{field.Field.Name} ({field.TypeName ?? OwnLabel})" : field.Field.Name,
                values.GetValueOrDefault((field.Owner, field.Field.Id))))
            .ToList();
    }

    private sealed record TypeFields(string Id, string Name, IReadOnlyList<FieldDefinition> Fields);
}
