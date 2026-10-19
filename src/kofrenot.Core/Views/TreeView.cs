using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

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

    /// <summary>The path as one text, the ids joined by '/': what tells this item from the node's others.</summary>
    public string Key => KeyOf(Path);

    /// <summary>The <see cref="Key"/> of the item it is shown under, or null for the root.</summary>
    public string? ParentKey => Parent is null ? null : KeyOf(Path.SkipLast(1));

    /// <summary>The <see cref="Key"/> of each item above it, from the root: what to expand to show it.</summary>
    public IEnumerable<string> AncestorKeys => Enumerable.Range(1, Path.Count - 1).Select(count => KeyOf(Path.Take(count)));

    private static string KeyOf(IEnumerable<Guid> path) => string.Join('/', path);
}

/// <summary>
/// A tree view: from its root, each node holds the nodes its links of the view's type lead to
/// (from it, or either way for a symmetric type), by name. Children are read from the graph when
/// asked, so a large tree costs only what is expanded, and changes show.
/// </summary>
public sealed class TreeView(KnowledgeGraph graph, TreeDefinition definition)
{
    private readonly TreeLinks _links = new(graph, definition.LinkType);

    /// <summary>Null when the root node is unknown (removed, or not synced yet).</summary>
    public TreeItem? Root => graph.FindNode(definition.Root) is { } root ? ItemOf(root, []) : null;

    /// <summary>
    /// Every place in the whole tree, expanded or not, whose node's name matches <paramref name="text"/>
    /// (<see cref="GraphSearch.MatcherOf"/>), in the order shown: a node, then its children. A node
    /// under several parents is found under each.
    /// </summary>
    public IReadOnlyList<TreeItem> Find(string text)
    {
        var matches = GraphSearch.MatcherOf(text);
        var found = new List<TreeItem>();
        var next = new Stack<TreeItem>();
        if (Root is { } root)
        {
            next.Push(root);
        }

        while (next.TryPop(out var item))
        {
            if (matches(item.Node.Name))
            {
                found.Add(item);
            }

            foreach (var child in ChildrenOf(item).Reverse())
            {
                next.Push(child);
            }
        }

        return found;
    }

    public IReadOnlyList<TreeItem> ChildrenOf(TreeItem item) => item.CanExpand
        ? NodeOrder.ByName(_links.ChildrenOf(item.Node.Id)).Select(child => ItemOf(child, item.Path)).ToList()
        : [];

    private TreeItem ItemOf(Node node, IReadOnlyList<Guid> parentPath) =>
        new(node, [.. parentPath, node.Id], !parentPath.Contains(node.Id) && _links.ToChildren(node.Id).Any());
}
