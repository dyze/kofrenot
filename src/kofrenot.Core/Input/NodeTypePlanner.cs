using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>Why a node type cannot be added or changed.</summary>
public enum NodeTypeProblem
{
    None,

    /// <summary>No name typed.</summary>
    Empty,

    /// <summary>Another node type has the name (<see cref="NodeTypePlan.Detail"/>).</summary>
    Taken,

    /// <summary>A field has no name.</summary>
    FieldEmpty,

    /// <summary>Two fields have one name (<see cref="NodeTypePlan.Detail"/>).</summary>
    FieldTwice,

    /// <summary>A choice field has no option, an empty one, or one twice (<see cref="NodeTypePlan.Detail"/>).</summary>
    BadChoices,

    /// <summary>A change to what the type is already.</summary>
    Unchanged,
}

/// <summary>A field as typed on the node types screen; <see cref="Id"/> is null for a field added there.</summary>
public sealed record FieldDraft(string? Id, string Name, FieldKind Kind, IReadOnlyList<string> Choices)
{
    public FieldDraft(string? id, string name, FieldKind kind)
        : this(id, name, kind, [])
    {
    }

    /// <summary>A field of a type as it is, to change it.</summary>
    public static FieldDraft Of(FieldDefinition field) => new(field.Id, field.Name, field.Kind, field.Choices);
}

/// <summary>A node type as typed on the node types screen: a name, an icon (<see cref="TypeIcons"/>, or none), a colour and fields.</summary>
public sealed record NodeTypeDraft(string Name, string? Icon, NodeColor Color, IReadOnlyList<FieldDraft> Fields)
{
    /// <summary>A type as it is, to change it.</summary>
    public static NodeTypeDraft Of(NodeType type) => new(type.Name, type.Icon, type.Color, type.Fields.Select(FieldDraft.Of).ToList());
}

/// <summary>Adds a node type of the user's, or changes <see cref="Edited"/>; <see cref="Change"/> is null when it cannot (<see cref="Problem"/>).</summary>
public sealed record NodeTypePlan(CustomNodeType? Edited, string Name, NodeTypeProblem Problem, string? Detail, NodeTypeDefined? Change)
{
    public bool CanApply => Change is not null;

    /// <summary>What applying does, or why it cannot.</summary>
    public string Status => Problem switch
    {
        NodeTypeProblem.Empty => "Type a name.",
        NodeTypeProblem.Taken => $"“{Detail}” is a node type already.",
        NodeTypeProblem.FieldEmpty => "Name every field.",
        NodeTypeProblem.FieldTwice => $"Two fields are named “{Detail}”.",
        NodeTypeProblem.BadChoices => $"Give “{Detail}” its options, each once.",
        NodeTypeProblem.Unchanged => string.Empty,
        _ when Edited is null => $"Adds “{Name}”.",
        _ => $"Changes “{Edited.Type.Name}”: its nodes show its fields as they are now, a field taken out keeping its values as their own.",
    };
}

/// <summary>Removes <see cref="Type"/>: its <see cref="Nodes"/> lose it, keeping their values as their own fields.</summary>
public sealed record NodeTypeRemoval(CustomNodeType Type, int Nodes, ChangesRemoved Change)
{
    /// <summary>What removing does, asked first.</summary>
    public string Question => Nodes switch
    {
        0 => $"Remove the node type “{Type.Type.Name}”?",
        1 => $"Remove the node type “{Type.Type.Name}”? 1 node has it: it keeps its values, as its own fields.",
        _ => $"Remove the node type “{Type.Type.Name}”? {Nodes} nodes have it: they keep their values, as their own fields.",
    };
}

/// <summary>
/// Turns a node type typed on the node types screen into the change that adds or changes it, and
/// a type into the change that removes it. A field keeps its id when it is renamed, so its values
/// follow it. A change replaces every current definition, so it resolves a conflict.
/// </summary>
public sealed class NodeTypePlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public NodeTypePlan Plan(NodeTypeDraft draft) => Plan(edited: null, draft);

    public NodeTypePlan Edit(CustomNodeType edited, NodeTypeDraft draft) => Plan(edited, draft);

    public NodeTypeRemoval Remove(CustomNodeType type)
    {
        var nodes = graph.Nodes.Count(node => graph.HasType(node.Id, type.Type.Id));
        return new NodeTypeRemoval(type, nodes, new ChangesRemoved(Guid.NewGuid(), _clock.GetUtcNow(), type.Ids));
    }

    private NodeTypePlan Plan(CustomNodeType? edited, NodeTypeDraft draft)
    {
        var name = draft.Name.Trim();
        NodeTypePlan Refused(NodeTypeProblem problem, string? detail = null) => new(edited, name, problem, detail, Change: null);

        if (name.Length == 0)
        {
            return Refused(NodeTypeProblem.Empty);
        }

        if (graph.Types.NodeTypes.FirstOrDefault(type => type.Id != edited?.Type.Id && NameKey.Of(type.Name) == NameKey.Of(name)) is { } taken)
        {
            return Refused(NodeTypeProblem.Taken, taken.Name);
        }

        var fields = draft.Fields.Select(FieldOf).ToList();
        if (fields.Any(field => field.Kind.IsContent()))
        {
            throw new ArgumentException("A type of the user's has no content field", nameof(draft));
        }

        if (fields.Any(field => field.Name.Length == 0))
        {
            return Refused(NodeTypeProblem.FieldEmpty);
        }

        if (fields.GroupBy(field => NameKey.Of(field.Name)).FirstOrDefault(same => same.Count() > 1) is { } twice)
        {
            return Refused(NodeTypeProblem.FieldTwice, twice.First().Name);
        }

        if (fields.FirstOrDefault(field => field.Kind == FieldKind.Choice && !AreValid(field.Choices)) is { } badChoices)
        {
            return Refused(NodeTypeProblem.BadChoices, badChoices.Name);
        }

        var icon = TypeIcons.Find(draft.Icon);
        var color = NodeColorNames.Of(draft.Color);
        if (edited is { IsConflict: false, Type: var current }
            && (current.Name, current.Icon, current.Color) == (name, icon, draft.Color) && current.Fields.SequenceEqual(fields))
        {
            return Refused(NodeTypeProblem.Unchanged);
        }

        var id = Guid.NewGuid();
        var change = new NodeTypeDefined(
            id,
            _clock.GetUtcNow(),
            edited?.Type.Id ?? id.ToString(),
            name,
            icon,
            color,
            fields,
            edited?.Definitions.Select(definition => definition.Id).ToList() ?? []);
        return new NodeTypePlan(edited, name, NodeTypeProblem.None, Detail: null, change);
    }

    private static FieldDefinition FieldOf(FieldDraft draft) => new(
        draft.Id ?? Guid.NewGuid().ToString(),
        draft.Name.Trim(),
        draft.Kind,
        draft.Kind == FieldKind.Choice ? draft.Choices.Select(choice => choice.Trim()).ToList() : []);

    private static bool AreValid(IReadOnlyList<string> choices) =>
        choices.Count > 0 && choices.All(choice => choice.Length > 0) && choices.Distinct(StringComparer.CurrentCultureIgnoreCase).Count() == choices.Count;
}
