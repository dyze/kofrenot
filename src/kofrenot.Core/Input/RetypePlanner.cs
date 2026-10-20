using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>Why a node cannot be given the type chosen.</summary>
public enum RetypeProblem
{
    None,

    /// <summary>Only a generic node gets a more specific type: a note's or a dataset's content would be hidden.</summary>
    NotGeneric,

    /// <summary>The type is the current one, and there is no conflict to resolve.</summary>
    Unchanged,
}

/// <summary>Gives <see cref="Node"/> the type <see cref="Type"/>; <see cref="Change"/> is null when it cannot (<see cref="Problem"/>).</summary>
public sealed record RetypePlan(Node Node, NodeType Type, RetypeProblem Problem, NodeRetyped? Change);

/// <summary>
/// Turns a type chosen for a node into the change that gives it. A generic node can become a note,
/// a dataset or a view. The change replaces every current type, so choosing one of the types of a
/// node in conflict resolves the conflict.
/// </summary>
public sealed class RetypePlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>The types a generic node can be given; none for another node.</summary>
    public static IReadOnlyList<NodeType> TargetsOf(Node node) =>
        node.Type == TypeCatalog.Generic.Id ? [TypeCatalog.Note, TypeCatalog.Dataset, TypeCatalog.View] : [];

    public RetypePlan Plan(Node node, NodeType type)
    {
        var current = graph.TypesOf(node.Id);
        var problem = current switch
        {
            [{ } only] when only.Type == type.Id => RetypeProblem.Unchanged,
            [_, _, ..] when current.Any(version => version.Type == type.Id) => RetypeProblem.None,
            _ when !TargetsOf(node).Contains(type) => RetypeProblem.NotGeneric,
            _ => RetypeProblem.None,
        };
        var change = problem == RetypeProblem.None
            ? new NodeRetyped(Guid.NewGuid(), _clock.GetUtcNow(), node.Id, type.Id, current.Select(version => version.Id).ToList())
            : null;
        return new RetypePlan(node, type, problem, change);
    }
}
