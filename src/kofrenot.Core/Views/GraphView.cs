using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Views;

/// <summary>A link a graph view draws, with the role its type has in the view.</summary>
public sealed record GraphEdge(Link Link, GraphRole Role);

/// <summary>
/// What a graph view shows: its nodes, the root first, then in the order they were reached, and the
/// links between them. <see cref="IsCut"/> when there were more than <see cref="GraphView.MaxNodes"/>.
/// </summary>
public sealed record GraphContent(IReadOnlyList<Node> Nodes, IReadOnlyList<GraphEdge> Edges, bool IsCut)
{
    public Node Root => Nodes[0];
}

/// <summary>
/// A graph view: from its root, the nodes reached within the view's depth through its link types,
/// either way, and kept by its node types; then every link of those types between them. Read from
/// the graph each time, so it shows changes.
/// </summary>
public sealed class GraphView(KnowledgeGraph graph, GraphDefinition definition)
{
    /// <summary>The most nodes shown: a graph is drawn whole, and laid out at each change.</summary>
    public const int MaxNodes = 500;

    // A symmetric type has no direction to rank by: its links are free.
    private readonly Dictionary<string, GraphRole> _roles = definition.Links
        .GroupBy(link => link.LinkType)
        .ToDictionary(
            links => links.Key,
            links => links.First().Role is GraphRole.Down or GraphRole.Up && graph.Types.FindLinkType(links.Key)?.IsSymmetric == true ? GraphRole.Free : links.First().Role);

    /// <summary>Null when the root node is unknown (removed, or not synced yet).</summary>
    public GraphContent? Read()
    {
        if (graph.FindNode(definition.Root) is not { } root)
        {
            return null;
        }

        var nodes = new List<Node> { root };
        var reached = new HashSet<Guid> { root.Id };
        var next = new Queue<(Node Node, int Depth)>([(root, 0)]);
        var isCut = false;
        while (next.TryDequeue(out var current) && !isCut)
        {
            if (current.Depth == definition.Depth)
            {
                continue;
            }

            foreach (var neighbour in NeighboursOf(current.Node).Where(neighbour => !reached.Contains(neighbour.Id)))
            {
                if (nodes.Count == MaxNodes)
                {
                    isCut = true;
                    break;
                }

                reached.Add(neighbour.Id);
                nodes.Add(neighbour);
                next.Enqueue((neighbour, current.Depth + 1));
            }
        }

        return new GraphContent(nodes, EdgesBetween(nodes, reached), isCut);
    }

    /// <summary>
    /// A node's values of the fields the view names, in the view's order, as "Born: 8 Oct 1920";
    /// none for a field without a value.
    /// </summary>
    public IReadOnlyList<string> CardLinesOf(Node node)
    {
        var fields = NodeFields.Of(graph, node.Id).Where(field => field.HasValue).ToList();
        return definition.Fields
            .SelectMany(name => fields.Where(field => NameKey.Of(field.Name) == NameKey.Of(name)))
            .Select(field => $"{field.Name}: {field.Display}")
            .Distinct()
            .ToList();
    }

    // The nodes a node's links of the view's types lead to, either way, of the view's node types, by name.
    private IReadOnlyList<Node> NeighboursOf(Node node) => NodeOrder.ByName(LinksOf(node.Id)
        .Select(link => graph.FindNode(link.Other(node.Id))!)
        .Where(IsShown)
        .DistinctBy(neighbour => neighbour.Id));

    private IEnumerable<Link> LinksOf(Guid node) => graph.LinksOf(node).Where(link => _roles.ContainsKey(link.Type) && link.From != link.To);

    private bool IsShown(Node node) => definition.NodeTypes.Count == 0 || definition.NodeTypes.Any(type => graph.HasType(node.Id, type));

    private List<GraphEdge> EdgesBetween(IEnumerable<Node> nodes, HashSet<Guid> shown)
    {
        var drawn = new HashSet<Guid>();
        return nodes
            .SelectMany(node => LinksOf(node.Id))
            .Where(link => shown.Contains(link.From) && shown.Contains(link.To) && drawn.Add(link.Id))
            .Select(link => new GraphEdge(link, _roles[link.Type]))
            .ToList();
    }
}
