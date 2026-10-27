using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Browse;

/// <summary>
/// A field shown in a node's details. <see cref="Owner"/> and <see cref="Field"/> say where its
/// values are written (<see cref="FieldSet"/>). <see cref="Type"/> is the type it comes from, or
/// null for a field of the node's own, which includes a field that left its type, or a type the
/// node no longer has, keeping its value. <see cref="Label"/> is its name, with where it comes from
/// when another field has that name: <c>Born (Person)</c>, <c>Born (own)</c>.
/// </summary>
/// <param name="Values">Its current values; null before it is set.</param>
public sealed record NodeField(string Owner, string Field, string Name, FieldKind Kind, IReadOnlyList<string> Choices, NodeType? Type, string Label, FieldVersions? Values)
{
    public bool IsOwn => Type is null;

    /// <summary>The value shown; null when it has none.</summary>
    public string? Value => Values?.Shown.Value;

    /// <summary>The value as shown (<see cref="FieldValue.Display"/>); empty when it has none.</summary>
    public string Display => Value is { } value ? FieldValue.Display(Kind, value) : string.Empty;

    /// <summary>The values set on two devices before they synced, oldest first, as shown; empty without a conflict.</summary>
    public IReadOnlyList<string> Conflict => Values is { IsConflict: true } values
        ? values.Current.Select(version => version.Value is { } value ? FieldValue.Display(Kind, value) : "(none)").ToList()
        : [];

    /// <summary>The ids of its current values: what a new value replaces.</summary>
    public IReadOnlyList<Guid> Current => Values?.Current.Select(version => version.Id).ToList() ?? [];
}

/// <summary>
/// The fields of a node: those of each of its types, in their order, then its own, in the order
/// they were first set. A type's field shows empty until it is set; an own field only shows with a
/// value. Content (a note's text, a dataset's file, a view's definition) is shown apart, not here.
/// </summary>
public static class NodeFields
{
    /// <summary>The label of a field of the node's own whose name another field has.</summary>
    public const string OwnLabel = "own";

    public static IReadOnlyList<NodeField> Of(KnowledgeGraph graph, Guid node)
    {
        var values = graph.FieldsOf(node).ToDictionary(field => (field.Owner, field.Field));
        var fields = new List<(string Owner, FieldDefinition Field, NodeType? Type)>();
        foreach (var type in graph.TypeIdsOf(node).Select(graph.Types.FindNodeType).OfType<NodeType>())
        {
            fields.AddRange(type.Fields.Where(field => field.Kind.IsSmall()).Select(field => (type.Id, field, (NodeType?)type)));
        }

        var ofTypes = fields.Select(field => (field.Owner, field.Field.Id)).ToHashSet();
        fields.AddRange(graph.FieldsOf(node)
            .Where(field => !ofTypes.Contains((field.Owner, field.Field)) && field.Shown.Value is not null)
            .Select(field => (field.Owner, new FieldDefinition(field.Field, field.Shown.Name, field.Shown.Kind), (NodeType?)null)));

        var sameName = fields.GroupBy(field => NameKey.Of(field.Field.Name)).Where(names => names.Count() > 1).Select(names => names.Key).ToHashSet();
        return fields
            .Select(field => new NodeField(
                field.Owner,
                field.Field.Id,
                field.Field.Name,
                field.Field.Kind,
                field.Field.Choices,
                field.Type,
                sameName.Contains(NameKey.Of(field.Field.Name)) ? $"{field.Field.Name} ({field.Type?.Name ?? OwnLabel})" : field.Field.Name,
                values.GetValueOrDefault((field.Owner, field.Field.Id))))
            .ToList();
    }
}
