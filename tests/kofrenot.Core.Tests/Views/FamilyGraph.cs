namespace Kofrenot.Core.Tests.Views;

/// <summary>A graph built by hand for the graph view tests: people, "parent of" (directed) and "married to" (symmetric) links.</summary>
internal sealed class FamilyGraph
{
    public const string Parent = "parent";
    public const string Spouse = "spouse";

    private readonly TreeGraph _tree = new();

    public FamilyGraph()
    {
        _tree.Graph.Apply(new LinkTypeDefined(Guid.NewGuid(), TreeGraph.Start, Parent, "parent of", "child of", []));
        _tree.Graph.Apply(new LinkTypeDefined(Guid.NewGuid(), TreeGraph.Start, Spouse, "married to", null, []));
    }

    public static GraphLink[] Family => [new(Parent, GraphRole.Down), new(Spouse, GraphRole.Beside)];

    public KnowledgeGraph Graph => _tree.Graph;

    public Node Add(string name, string? type = null) => _tree.Add(name, type);

    public Link ParentOf(Node parent, Node child) => _tree.Link(Parent, parent, child);

    public Link Married(Node first, Node second) => _tree.Link(Spouse, first, second);

    public Link Link(string type, Node from, Node to) => _tree.Link(type, from, to);

    public GraphDefinition Definition(Node root, int depth = 3, GraphLayoutKind layout = GraphLayoutKind.Layered, IReadOnlyList<GraphLink>? links = null, IReadOnlyList<string>? nodeTypes = null, IReadOnlyList<string>? fields = null) =>
        new(root.Id, depth, layout, links ?? Family, nodeTypes ?? [], fields ?? []);

    public GraphContent Read(GraphDefinition definition) => new GraphView(Graph, definition).Read()!;
}
