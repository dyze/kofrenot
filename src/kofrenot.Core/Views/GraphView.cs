using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.Core.Views;

/// <summary>
/// A link a graph view draws, with the role its type has in the view, the colour of its type
/// (<see cref="GraphLegendItem.Color"/>), whether it has a direction (not for a symmetric type), and
/// its name as read from its <see cref="Link.From"/> node.
/// </summary>
public sealed record GraphEdge(Link Link, GraphRole Role, int Color, bool IsDirected, string Name, string? Qualifier = null)
{
    /// <summary>Its name with its qualifier shown: "wrote · 1965".</summary>
    public string Label => Qualifier is { } shown ? $"{Name} · {shown}" : Name;
}

/// <summary>A link type a graph view follows, as its legend shows it: its name, its role, and its colour, the type's place among the view's link types.</summary>
public sealed record GraphLegendItem(string LinkType, string Name, GraphRole Role, int Color);

/// <summary>
/// What a graph view shows: its nodes, the root first, then in the order they were reached, and the
/// links between them. <see cref="IsCut"/> when there were more than <see cref="GraphView.MaxNodes"/>.
/// </summary>
public sealed record GraphContent(IReadOnlyList<Node> Nodes, IReadOnlyList<GraphEdge> Edges, bool IsCut)
{
    public Node Root => Nodes[0];

    /// <summary>The nodes whose name matches <paramref name="text"/> (<see cref="GraphSearch.MatcherOf"/>), in the order shown.</summary>
    public IReadOnlyList<Node> Find(string text)
    {
        var matches = GraphSearch.MatcherOf(text);
        return Nodes.Where(node => matches(node.Name)).ToList();
    }
}

/// <summary>
/// A graph view: from its root, the nodes reached within the view's depth through its link types,
/// either way, and kept by its node types; then the nodes placed by hand in <paramref name="view"/>
/// that none of those reaches (added there unlinked, or unlinked since), kept by its node types too;
/// then every link of those types between them. Read from the graph each time, so it shows changes.
/// </summary>
/// <param name="view">The view node, whose placements (<see cref="KnowledgeGraph.PositionsIn"/>) keep nodes in it; null for none.</param>
public sealed class GraphView(KnowledgeGraph graph, GraphDefinition definition, Guid? view = null)
{
    /// <summary>The most nodes shown: a graph is drawn whole, and laid out at each change.</summary>
    public const int MaxNodes = 500;

    // A symmetric type has no direction to rank by: its links are free.
    private readonly Dictionary<string, GraphRole> _roles = definition.Links
        .GroupBy(link => link.LinkType)
        .ToDictionary(
            links => links.Key,
            links => links.First().Role is GraphRole.Down or GraphRole.Up && graph.Types.FindLinkType(links.Key)?.IsSymmetric == true ? GraphRole.Free : links.First().Role);

    private IReadOnlyList<GraphLegendItem>? _legend;

    /// <summary>The view's link types, each once, in the view's order: the colours of its lines.</summary>
    public IReadOnlyList<GraphLegendItem> Legend => _legend ??= definition.Links
        .Select(link => link.LinkType)
        .Distinct()
        .Select((type, index) => new GraphLegendItem(type, graph.Types.FindLinkType(type)?.Forward.Name ?? type, _roles[type], index))
        .ToList();

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

        foreach (var placed in PlacedNotReached(reached))
        {
            if (nodes.Count == MaxNodes)
            {
                isCut = true;
                break;
            }

            reached.Add(placed.Id);
            nodes.Add(placed);
        }

        return new GraphContent(nodes, EdgesBetween(nodes, reached), isCut);
    }

    // The nodes placed in the view that the links do not reach, of the view's node types, by name.
    private IReadOnlyList<Node> PlacedNotReached(HashSet<Guid> reached) => view is { } shownIn
        ? NodeOrder.ByName(graph.PositionsIn(shownIn).Keys.Where(node => !reached.Contains(node)).Select(graph.FindNode).OfType<Node>().Where(IsShown))
        : [];

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

    private int ColorOf(string linkType) => Legend.First(item => item.LinkType == linkType).Color;

    private List<GraphEdge> EdgesBetween(IEnumerable<Node> nodes, HashSet<Guid> shown)
    {
        var drawn = new HashSet<Guid>();
        return nodes
            .SelectMany(node => LinksOf(node.Id))
            .Where(link => shown.Contains(link.From) && shown.Contains(link.To) && drawn.Add(link.Id))
            .Select(link => new GraphEdge(
                link, _roles[link.Type], ColorOf(link.Type), graph.Types.FindLinkType(link.Type)?.IsSymmetric == false, graph.Types.LinkName(link, link.From), NodeFields.ShownOf(graph, link)?.Display))
            .ToList();
    }
}
