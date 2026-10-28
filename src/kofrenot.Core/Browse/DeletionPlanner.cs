using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Browse;

/// <summary>A node deleted from its details, with its links and pins: <see cref="Question"/> asks first, naming what goes.</summary>
public sealed record NodeDeletion(Node Node, int Links, ChangesRemoved Change)
{
    public string Question =>
        $"Delete “{Node.Name}”{(Links == 0 ? string.Empty : Links == 1 ? " and its link" : $" and its {Links} links")}?\n\nIt is deleted on every device; its history goes with it.";
}

/// <summary>
/// Deletes one node from its details (not a subtree, as a tree does: <see cref="Views.DeletePlanner"/>),
/// or one link. A removal is final: what is added again later is new.
/// </summary>
public sealed class DeletionPlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public NodeDeletion Node(Node node)
    {
        var links = graph.LinksOf(node.Id);
        IReadOnlyList<Guid> removed = [node.Id, .. links.SelectMany(graph.CopiesOf), .. graph.PinsOf(node.Id)];
        return new NodeDeletion(node, links.Count, new ChangesRemoved(Guid.NewGuid(), _clock.GetUtcNow(), removed));
    }

    /// <summary>Removes a link, with its copies (the same link added on two devices).</summary>
    public ChangesRemoved Link(Link link) => new(Guid.NewGuid(), _clock.GetUtcNow(), graph.CopiesOf(link));
}
