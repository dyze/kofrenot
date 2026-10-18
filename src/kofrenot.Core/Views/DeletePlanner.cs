using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Views;

/// <summary>A node deleted that also has a parent outside what is deleted: it disappears from there too.</summary>
public sealed record OtherParent(Node Node, Node Parent);

/// <summary>
/// What deleting a node does, shown before: the nodes deleted (it and its whole subtree, by name),
/// the links removed with them, and the other parents they have. <see cref="Change"/> removes them all.
/// </summary>
public sealed record DeletePlan(IReadOnlyList<Node> Nodes, IReadOnlyList<Link> Links, IReadOnlyList<OtherParent> OtherParents, ChangesRemoved Change);

/// <summary>Deletes a node of a tree with its whole subtree, every node in it, even one that has another parent.</summary>
public sealed class DeletePlanner(KnowledgeGraph graph, string linkType, TimeProvider? clock = null)
{
    private readonly TreeLinks _links = new(graph, linkType);
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public DeletePlan Plan(TreeItem item)
    {
        var subtree = _links.Subtree(item.Node.Id);
        var deleted = subtree.ToHashSet();
        var links = subtree.SelectMany(graph.LinksOf).DistinctBy(link => link.Id).ToList();
        var otherParents = subtree
            .SelectMany(node => _links.ParentsOf(node)
                .Where(parent => !deleted.Contains(parent) && !(node == item.Node.Id && parent == item.Parent))
                .Select(parent => new OtherParent(NodeOf(node), NodeOf(parent))))
            .ToList();
        var removed = subtree.Concat(links.SelectMany(graph.CopiesOf)).ToList();

        return new DeletePlan(
            NodeOrder.ByName(subtree.Select(NodeOf)),
            links,
            otherParents,
            new ChangesRemoved(Guid.NewGuid(), _clock.GetUtcNow(), removed));

        Node NodeOf(Guid id) => id == item.Node.Id ? item.Node : graph.FindNode(id)!;
    }
}
