using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Views;

/// <summary>
/// How a tree reads the graph: the links of its type lead from a node to its children (either way
/// for a symmetric type). Shared by the tree view and the operations on it, so they agree.
/// </summary>
internal sealed class TreeLinks(KnowledgeGraph graph, string linkType)
{
    private readonly bool _symmetric = graph.Types.FindLinkType(linkType)?.IsSymmetric ?? false;

    public IEnumerable<Link> ToChildren(Guid node) =>
        graph.LinksOf(node).Where(link => link.Type == linkType && (_symmetric || link.From == node));

    public IEnumerable<Node> ChildrenOf(Guid node) => ToChildren(node).Select(link => graph.FindNode(link.Other(node))!);

    public IEnumerable<Guid> ParentsOf(Guid node) =>
        graph.LinksOf(node).Where(link => link.Type == linkType && (_symmetric || link.To == node)).Select(link => link.Other(node));

    /// <summary>The node and every node under it, each once, the node first: cycles and several parents are read once.</summary>
    public IReadOnlyList<Guid> Subtree(Guid root)
    {
        var found = new List<Guid> { root };
        var seen = new HashSet<Guid> { root };
        for (var index = 0; index < found.Count; index++)
        {
            foreach (var child in ChildrenOf(found[index]).Where(child => seen.Add(child.Id)))
            {
                found.Add(child.Id);
            }
        }

        return found;
    }
}
