using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>Why a node's types cannot change as chosen.</summary>
public enum TypeSetProblem
{
    None,

    /// <summary>The node has the type added already, or not the type removed; or the type is <see cref="TypeCatalog.Generic"/>.</summary>
    Unchanged,
}

/// <summary>
/// Adds <see cref="Type"/> to the types of <see cref="Node"/>, or takes it out (<see cref="IsAdded"/>
/// false); <see cref="Changes"/> is empty when it cannot (<see cref="Problem"/>).
/// </summary>
/// <param name="Hidden">What the node shows now and will no longer show (its note's text, its dataset's file, what its view shows), and how to show it again; null when nothing is hidden.</param>
public sealed record TypeSetPlan(Node Node, NodeType Type, bool IsAdded, TypeSetProblem Problem, IReadOnlyList<Change> Changes, string? Hidden = null);

/// <summary>
/// Turns a type added to a node, or taken out, into its changes. A node has a set of types, each
/// bringing its fields; <see cref="TypeCatalog.Generic"/> is what a node with no other type is, so
/// adding a type takes it out. A node has one type with content (a note, a dataset, a view) at
/// most: adding one takes out the other. Nothing is lost, as a vault only grows: the content of a
/// type taken out stays in the vault, hidden, and shows again when the node takes that type back.
/// </summary>
public sealed class TypeSetPlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public TypeSetPlan Add(Node node, NodeType type)
    {
        if (type == TypeCatalog.Generic || graph.HasType(node.Id, type.Id))
        {
            return new TypeSetPlan(node, type, IsAdded: true, TypeSetProblem.Unchanged, []);
        }

        var now = _clock.GetUtcNow();
        var replaced = graph.TypesOf(node.Id)
            .Where(version => version.Type == TypeCatalog.Generic.Id || (type.HasContent && HasContent(version.Type)))
            .ToList();
        IReadOnlyList<Change> changes = replaced.Count == 0
            ? [new NodeTypeAdded(Guid.NewGuid(), now, node.Id, type.Id)]
            : [new NodeTypeRemoved(Guid.NewGuid(), now, node.Id, replaced.Select(version => version.Id).ToList()), new NodeTypeAdded(Guid.NewGuid(), now, node.Id, type.Id)];
        var hidden = replaced.Any(version => version.Type == node.Type) ? HiddenBy(node) : null;
        return new TypeSetPlan(node, type, IsAdded: true, TypeSetProblem.None, changes, hidden);
    }

    public TypeSetPlan Remove(Node node, NodeType type)
    {
        var given = graph.TypesOf(node.Id).Where(version => version.Type == type.Id).Select(version => version.Id).ToList();
        if (type == TypeCatalog.Generic || given.Count == 0)
        {
            return new TypeSetPlan(node, type, IsAdded: false, TypeSetProblem.Unchanged, []);
        }

        var removal = new NodeTypeRemoved(Guid.NewGuid(), _clock.GetUtcNow(), node.Id, given);
        return new TypeSetPlan(node, type, IsAdded: false, TypeSetProblem.None, [removal], type.Id == node.Type ? HiddenBy(node) : null);
    }

    private bool HasContent(string type) => graph.Types.FindNodeType(type)?.HasContent ?? false;

    /// <summary>What the node shows as its type with content, hidden once it no longer has it; null when it has nothing to show.</summary>
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
