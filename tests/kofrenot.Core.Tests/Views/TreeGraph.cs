namespace Kofrenot.Core.Tests.Views;

/// <summary>A graph built by hand for the tree tests: nodes, and "contains" links between them.</summary>
internal sealed class TreeGraph
{
    public static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    public KnowledgeGraph Graph { get; } = new();

    public Node Add(string name, string? type = null)
    {
        var node = new Node(Guid.NewGuid(), type ?? TypeCatalog.Generic.Id, name, Start);
        Graph.Apply(new NodeAdded(Guid.NewGuid(), Start, node));
        return node;
    }

    public Link Contains(Node parent, Node child) => Link(TypeCatalog.Contains.Id, parent, child);

    public Link Link(string type, Node from, Node to)
    {
        var link = new Link(Guid.NewGuid(), type, from.Id, to.Id, Start);
        Graph.Apply(new LinkAdded(Guid.NewGuid(), Start, link));
        return link;
    }

    /// <summary>The item of <paramref name="node"/> shown under <paramref name="parent"/> (null: as the root).</summary>
    public static TreeItem Item(Node node, Node? parent = null) =>
        new(node, parent is null ? [node.Id] : [parent.Id, node.Id], CanExpand: true);

    public void Apply(IEnumerable<Change> changes)
    {
        foreach (var change in changes)
        {
            Graph.Apply(change);
        }
    }
}
