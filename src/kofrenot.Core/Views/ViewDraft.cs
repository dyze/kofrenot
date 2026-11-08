using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.Core.Views;

public enum ViewDisplay
{
    Tree,
    Flat,
    Graph,
    Query,
}

/// <summary>
/// A view's definition as edited (on the creation screen, or later): a tree (from <see cref="Root"/>,
/// through <see cref="LinkType"/>), a list (of <see cref="NodeType"/>), or a graph (from
/// <see cref="Root"/>, as <see cref="Graph"/> says). A tree and a graph need their root; a graph,
/// a link type too; a query view (<see cref="Query"/>), a query that reads as one.
/// </summary>
public sealed record ViewDraft(ViewDisplay Display, Node? Root, LinkType LinkType, NodeType NodeType)
{
    public const int RootChoices = 8;

    public static ViewDraft Default { get; } = new(ViewDisplay.Tree, Root: null, TypeCatalog.Contains, TypeCatalog.Note);

    /// <summary>What a graph follows and shows, kept while another display is chosen.</summary>
    public GraphDraft Graph { get; init; } = GraphDraft.Default;

    /// <summary>The qualifier of <see cref="LinkType"/> a tree's children are sorted by (its field id); by name when null.</summary>
    public string? Order { get; init; }

    /// <summary>What a query view finds and shows, kept while another display is chosen.</summary>
    public QueryDraft Query { get; init; } = QueryDraft.Default;

    /// <summary>The definition, or null while a tree or a graph has no root, a graph no link type, or a query view no query.</summary>
    public ViewDefinition? Definition => Display switch
    {
        ViewDisplay.Tree => Root is null ? null : new TreeDefinition(Root.Id, LinkType.Id, Order),
        ViewDisplay.Graph => Root is null || Graph.Links.Count == 0 ? null : new GraphDefinition(Root.Id, Graph.Depth, Graph.Layout, Graph.Links, Graph.NodeTypes, Graph.Fields),
        ViewDisplay.Query => Query.Definition,
        _ => new FlatDefinition(NodeType.Id),
    };

    /// <summary>
    /// A saved definition, to edit it: a root removed (or not synced yet) is to choose again; a type
    /// this version does not know is kept by its id.
    /// </summary>
    public static ViewDraft Of(ViewDefinition definition, KnowledgeGraph graph) => definition switch
    {
        TreeDefinition tree => Default with
        {
            Root = graph.FindNode(tree.Root),
            LinkType = graph.Types.FindLinkType(tree.LinkType) ?? new LinkType(tree.LinkType, [tree.LinkType]),
            Order = tree.Order,
        },
        FlatDefinition flat => Default with
        {
            Display = ViewDisplay.Flat,
            NodeType = graph.Types.FindNodeType(flat.NodeType) ?? new NodeType(flat.NodeType, flat.NodeType),
        },
        GraphDefinition shown => Default with
        {
            Display = ViewDisplay.Graph,
            Root = graph.FindNode(shown.Root),
            Graph = new GraphDraft(shown.Depth, shown.Layout, shown.Links, shown.NodeTypes, shown.Fields),
        },
        QueryDefinition query => Default with
        {
            Display = ViewDisplay.Query,
            Query = QueryDraft.Of(query, graph),
        },
        _ => Default,
    };

    /// <summary>The qualifiers a tree through <see cref="LinkType"/> can be sorted by: its type's that are typed.</summary>
    public IReadOnlyList<Fields.FieldDefinition> OrderChoices(KnowledgeGraph graph) =>
        graph.LinkTypes.FieldsOf(LinkType.Id).Where(field => Fields.FieldKinds.IsSmall(field.Kind)).ToList();

    /// <summary>The nodes a tree can start from, found by name as in the input box.</summary>
    public static IReadOnlyList<SearchHit> FindRoots(KnowledgeGraph graph, string text) => GraphSearch.Find(graph, text, RootChoices);

    /// <summary>What a view shows, in a sentence: "A tree from “Projects” through “contains”", "A list of every Note".</summary>
    public static string Describe(ViewDefinition? definition, KnowledgeGraph graph) => definition switch
    {
        TreeDefinition tree => graph.FindNode(tree.Root) is { } root
            ? $"A tree from “{root.Name}” through “{LinkTypeName(graph, tree.LinkType)}”{OrderName(graph, tree)}"
            : "A tree from a node not here: removed, or not synced yet",
        FlatDefinition flat => $"A list of every {graph.Types.NodeTypeName(flat.NodeType)}",
        GraphDefinition shown => graph.FindNode(shown.Root) is { } root
            ? $"A graph from “{root.Name}” through {string.Join(", ", shown.Links.Select(link => $"“{LinkTypeName(graph, link.LinkType)}”"))}, {shown.Depth} {(shown.Depth == 1 ? "step" : "steps")} away"
            : "A graph from a node not here: removed, or not synced yet",
        QueryDefinition query => DescribeQuery(query, graph),
        _ => "No definition yet",
    };

    private static string DescribeQuery(QueryDefinition query, KnowledgeGraph graph)
    {
        if (query.Node is { } node && graph.FindNode(node) is null)
        {
            return "A query naming a node not here: removed, or not synced yet";
        }

        var columns = query.Columns.Count == 0 ? string.Empty : $", with {string.Join(", ", query.Columns.Select(column => $"“{QueryDraft.NameOf(column, query.LinkType, graph)}”"))}";
        return $"The nodes found by “{QueryDraft.TextOf(query, graph)}”{columns}";
    }

    private static string OrderName(KnowledgeGraph graph, TreeDefinition tree) =>
        tree.Order is { } order && graph.LinkTypes.FieldsOf(tree.LinkType).FirstOrDefault(field => field.Id == order) is { } field ? $", by “{field.Name}”" : string.Empty;

    private static string LinkTypeName(KnowledgeGraph graph, string linkType) => graph.Types.FindLinkType(linkType)?.Name ?? linkType;
}

/// <summary>
/// A graph's settings as edited: how far it goes, how it is laid out, the link types it follows
/// (each with its role), the node types it keeps (any when none) and the fields its cards show.
/// </summary>
public sealed record GraphDraft(int Depth, GraphLayoutKind Layout, IReadOnlyList<GraphLink> Links, IReadOnlyList<string> NodeTypes, IReadOnlyList<string> Fields)
{
    public const int DefaultDepth = 3;

    public static GraphDraft Default { get; } = new(DefaultDepth, GraphLayoutKind.Layered, [], [], []);

    /// <summary>The role a link type has, or null when the graph does not follow it.</summary>
    public GraphRole? RoleOf(string linkType) => Links.FirstOrDefault(link => link.LinkType == linkType)?.Role;

    /// <summary>Follows a link type with a role, in place of the one it had; or no longer follows it when null.</summary>
    public GraphDraft WithRole(string linkType, GraphRole? role)
    {
        var links = Links.ToList();
        var index = links.FindIndex(link => link.LinkType == linkType);
        if (index >= 0)
        {
            links.RemoveAt(index);
        }

        if (role is { } kept)
        {
            links.Insert(index >= 0 ? index : links.Count, new GraphLink(linkType, kept));
        }

        return this with { Links = links };
    }

    /// <summary>Keeps a node type, or no longer keeps it.</summary>
    public GraphDraft WithNodeType(string nodeType, bool isKept) => this with
    {
        NodeTypes = isKept ? [.. NodeTypes.Where(type => type != nodeType), nodeType] : [.. NodeTypes.Where(type => type != nodeType)],
    };

    /// <summary>The fields named in a text, separated by commas: "Born, Died".</summary>
    public GraphDraft WithFields(string text) => this with
    {
        Fields = [.. text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase)],
    };

    /// <summary>The depth, kept between <see cref="GraphDefinition.MinDepth"/> and <see cref="GraphDefinition.MaxDepth"/>.</summary>
    public GraphDraft WithDepth(int depth) => this with { Depth = Math.Clamp(depth, GraphDefinition.MinDepth, GraphDefinition.MaxDepth) };
}
