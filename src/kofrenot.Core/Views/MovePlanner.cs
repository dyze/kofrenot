using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Views;

public enum MoveProblem
{
    None,

    /// <summary>The root of a tree has no parent to leave.</summary>
    Root,

    /// <summary>The node is already under that parent.</summary>
    SameParent,

    /// <summary>The target is the node or under it: the node would contain itself.</summary>
    IntoItsSubtree,
}

/// <summary>The changes moving a node, or why it cannot move (then none).</summary>
public sealed record MovePlan(MoveProblem Problem, IReadOnlyList<Change> Changes);

/// <summary>
/// Moves a node of a tree under another node: the link from the parent it is shown under is
/// removed (every copy), and a link from the target added. Its other parents stay.
/// </summary>
public sealed class MovePlanner(KnowledgeGraph graph, string linkType, TimeProvider? clock = null)
{
    private readonly TreeLinks _links = new(graph, linkType);
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public MovePlan Plan(TreeItem item, Node target)
    {
        if (item.Parent is not { } parent)
        {
            return new MovePlan(MoveProblem.Root, []);
        }

        if (target.Id == parent)
        {
            return new MovePlan(MoveProblem.SameParent, []);
        }

        if (_links.Subtree(item.Node.Id).Contains(target.Id))
        {
            return new MovePlan(MoveProblem.IntoItsSubtree, []);
        }

        var now = _clock.GetUtcNow();
        var changes = new List<Change>();
        if (graph.FindLink(linkType, parent, item.Node.Id) is { } left)
        {
            changes.Add(new ChangesRemoved(Guid.NewGuid(), now, graph.CopiesOf(left)));
        }

        if (graph.FindLink(linkType, target.Id, item.Node.Id) is null)
        {
            changes.Add(new LinkAdded(Guid.NewGuid(), now, new Link(Guid.NewGuid(), linkType, target.Id, item.Node.Id, now)));
        }

        return new MovePlan(MoveProblem.None, changes);
    }
}
