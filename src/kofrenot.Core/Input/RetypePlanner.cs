using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>Why a node cannot be given the type chosen.</summary>
public enum RetypeProblem
{
    None,

    /// <summary>The type is the current one, and there is no conflict to resolve.</summary>
    Unchanged,
}

/// <summary>Gives <see cref="Node"/> the type <see cref="Type"/>; <see cref="Change"/> is null when it cannot (<see cref="Problem"/>).</summary>
/// <param name="Hidden">What the node shows now and will no longer show (its note's text, its dataset's file, what its view shows), and how to show it again; null when nothing is hidden.</param>
public sealed record RetypePlan(Node Node, NodeType Type, RetypeProblem Problem, NodeRetyped? Change, string? Hidden = null);

/// <summary>
/// Turns a type chosen for a node into the change that gives it. Any node can take any type:
/// nothing is lost, as a vault only grows. The content of the type it leaves (a note's versions, a
/// dataset's, a view's) stays in the vault, hidden, and shows again when it takes that type back.
/// The change replaces every current type, so choosing one of the types of a node in conflict
/// resolves the conflict.
/// </summary>
public sealed class RetypePlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public RetypePlan Plan(Node node, NodeType type)
    {
        var current = graph.TypesOf(node.Id);
        if (current is [{ } only] && only.Type == type.Id)
        {
            return new RetypePlan(node, type, RetypeProblem.Unchanged, Change: null);
        }

        var change = new NodeRetyped(Guid.NewGuid(), _clock.GetUtcNow(), node.Id, type.Id, current.Select(version => version.Id).ToList());
        return new RetypePlan(node, type, RetypeProblem.None, change, type.Id == node.Type ? null : HiddenBy(node));
    }

    /// <summary>What the node shows as its type, hidden once it has another; null when it has nothing to show.</summary>
    private string? HiddenBy(Node node) => node.Type switch
    {
        _ when node.Type == TypeCatalog.Note.Id && graph.NoteVersionsOf(node.Id).Count > 0 =>
            "Its text is hidden; making it a note again shows it.",
        _ when node.Type == TypeCatalog.Dataset.Id && graph.DatasetVersionsOf(node.Id) is [.., var latest] =>
            $"Its file {latest.File.Name} is hidden; making it a dataset again shows it.",
        _ when node.Type == TypeCatalog.View.Id && graph.ViewVersionsOf(node.Id).Count > 0 =>
            "What it shows is hidden, and its tab closes; making it a view again shows it.",
        _ => null,
    };
}
