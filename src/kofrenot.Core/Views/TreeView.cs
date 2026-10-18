using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Views;

/// <summary>
/// A node of a tree view, reached by <see cref="Path"/>: the ids from the root to it. A node with
/// several parents has one item under each. <see cref="CanExpand"/> is false without children, and
/// for a node already on its path: a cycle is cut there.
/// </summary>
public sealed record TreeItem(Node Node, IReadOnlyList<Guid> Path, bool CanExpand)
{
    /// <summary>The node it is shown under, or null for the root.</summary>
    public Guid? Parent => Path.Count > 1 ? Path[^2] : null;
}

/// <summary>
/// A tree view: from its root, each node holds the nodes its links of the view's type lead to
/// (from it, or either way for a symmetric type), by name. Children are read from the graph when
/// asked, so a large tree costs only what is expanded, and changes show.
/// </summary>
public sealed class TreeView(KnowledgeGraph graph, TreeDefinition definition)
{
    /// <summary>Null when the root node is unknown (removed, or not synced yet).</summary>
    public TreeItem? Root => graph.FindNode(definition.Root) is { } root ? ItemOf(root, []) : null;

    public IReadOnlyList<TreeItem> ChildrenOf(TreeItem item) => item.CanExpand
        ? NodeOrder.ByName(ChildNodes(item.Node.Id)).Select(child => ItemOf(child, item.Path)).ToList()
        : [];

    private TreeItem ItemOf(Node node, IReadOnlyList<Guid> parentPath) =>
        new(node, [.. parentPath, node.Id], !parentPath.Contains(node.Id) && ChildNodes(node.Id).Any());

    private IEnumerable<Node> ChildNodes(Guid node)
    {
        var symmetric = graph.Types.FindLinkType(definition.LinkType)?.IsSymmetric ?? false;
        return graph.LinksOf(node)
            .Where(link => link.Type == definition.LinkType && (symmetric || link.From == node))
            .Select(link => graph.FindNode(link.Other(node))!);
    }
}
