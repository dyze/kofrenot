using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>What copying a node type into the open vault does.</summary>
public enum TypeCopyOutcome
{
    /// <summary>The open vault does not have it (or had it, removed): it is added.</summary>
    Adds,

    /// <summary>The open vault has it, defined otherwise: its definition is replaced.</summary>
    Updates,

    /// <summary>The open vault has it as it is: nothing to write.</summary>
    Same,

    /// <summary>Another node type of the open vault has its name (<see cref="TypeCopy.TakenBy"/>): rename one first.</summary>
    NameTaken,
}

/// <summary>A node type to copy, what copying it does, and the change that does it (none when <see cref="Outcome"/> writes nothing).</summary>
public sealed record TypeCopy(CustomNodeType Type, TypeCopyOutcome Outcome, NodeType? TakenBy, NodeTypeDefined? Change)
{
    /// <summary>What copying it does, as shown beside it.</summary>
    public string Status => Outcome switch
    {
        TypeCopyOutcome.Adds => "Adds it",
        TypeCopyOutcome.Updates => "Updates it",
        TypeCopyOutcome.Same => "Already the same here",
        _ => $"“{TakenBy!.Name}” is a node type here already: rename one first",
    };
}

/// <summary>
/// Turns node types of another vault into the changes that copy them into the open one, keeping
/// their ids and their fields' ids: copied again, a type is updated rather than added twice, and
/// its fields' values on the nodes stay theirs. Only the types are copied, never values.
/// </summary>
public sealed class TypeCopyPlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public IReadOnlyList<TypeCopy> Plan(IReadOnlyList<CustomNodeType> types)
    {
        var now = _clock.GetUtcNow();
        return types.Select(type => Plan(type, now)).ToList();
    }

    private TypeCopy Plan(CustomNodeType copied, DateTimeOffset now)
    {
        var type = copied.Type;
        if (graph.Types.NodeTypes.FirstOrDefault(other => other.Id != type.Id && NameKey.Of(other.Name) == NameKey.Of(type.Name)) is { } taken)
        {
            return new TypeCopy(copied, TypeCopyOutcome.NameTaken, taken, Change: null);
        }

        var here = graph.NodeTypes.Find(type.Id);
        if (here is { IsConflict: false, Type: var current }
            && (current.Name, current.Icon, current.Color) == (type.Name, type.Icon, type.Color) && current.Fields.SequenceEqual(type.Fields))
        {
            return new TypeCopy(copied, TypeCopyOutcome.Same, TakenBy: null, Change: null);
        }

        var change = new NodeTypeDefined(
            Guid.NewGuid(),
            now,
            type.Id,
            type.Name,
            type.Icon,
            NodeColorNames.Of(type.Color),
            type.Fields,
            here?.Definitions.Select(definition => definition.Id).ToList() ?? []);
        return new TypeCopy(copied, here is null ? TypeCopyOutcome.Adds : TypeCopyOutcome.Updates, TakenBy: null, change);
    }
}
