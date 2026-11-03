using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.Core.Views;

/// <summary>The unlinked nodes shown: at most <see cref="UnlinkedNodes.MaxShown"/>, of <see cref="Count"/> found.</summary>
public sealed record UnlinkedList(IReadOnlyList<Node> Shown, int Count)
{
    public bool IsCut => Count > Shown.Count;
}

/// <summary>
/// The nodes no directed link leads to (a symmetric link, "relates to", makes no parent): what an
/// import without a destination leaves, to be put in a tree. Views and the roots of tree views are
/// tops on purpose: left out. The newest first; read from the graph each time, so it shows changes.
/// </summary>
public sealed class UnlinkedNodes(KnowledgeGraph graph, TimeProvider? clock = null)
{
    public const int MaxShown = 100;

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>
    /// The links putting <paramref name="nodes"/> under <paramref name="under"/>'s node. A node is
    /// left out when it is that node, already under it, or above it (it would contain itself).
    /// </summary>
    public IReadOnlyList<Change> PutUnder(IEnumerable<Node> nodes, ParentLink under)
    {
        var now = _clock.GetUtcNow();
        var links = new TreeLinks(graph, under.LinkType);
        return nodes
            .Where(node => graph.FindLink(under.LinkType, under.Parent.Id, node.Id) is null && !links.Subtree(node.Id).Contains(under.Parent.Id))
            .Select(node => (Change)new LinkAdded(Guid.NewGuid(), now, new Link(Guid.NewGuid(), under.LinkType, under.Parent.Id, node.Id, now)))
            .ToList();
    }

    /// <summary>The unlinked nodes whose name matches <paramref name="text"/> (<see cref="GraphSearch.MatcherOf"/>); all of them when it is empty.</summary>
    public UnlinkedList Find(string text)
    {
        var matches = text.Trim().Length == 0 ? (_ => true) : GraphSearch.MatcherOf(text);
        var roots = TreeRoots();
        var types = graph.Types;
        var found = graph.Nodes
            .Where(node => node.Type != TypeCatalog.View.Id && !roots.Contains(node.Id) && matches(node.Name) && !HasParent(node.Id, types))
            .OrderByDescending(node => node.Created)
            .ThenBy(node => node.Id)
            .ToList();
        return new UnlinkedList(found.Take(MaxShown).ToList(), found.Count);
    }

    private bool HasParent(Guid node, TypeCatalog types) =>
        graph.LinksOf(node).Any(link => link.To == node && types.FindLinkType(link.Type)?.IsSymmetric != true);

    private HashSet<Guid> TreeRoots() => graph.Nodes
        .Where(node => node.Type == TypeCatalog.View.Id)
        .SelectMany(view => graph.ViewVersionsOf(view.Id))
        .Select(version => version.Definition)
        .OfType<TreeDefinition>()
        .Select(tree => tree.Root)
        .ToHashSet();
}
