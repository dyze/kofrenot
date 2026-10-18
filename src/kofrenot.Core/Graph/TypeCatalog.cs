namespace Kofrenot.Core.Graph;

/// <summary>
/// The node and link types known to this version, offered by the creation screen. Built in for
/// now; user-defined types will be added to it later.
/// </summary>
public sealed class TypeCatalog
{
    public TypeCatalog(IReadOnlyList<NodeType> nodeTypes, IReadOnlyList<LinkType> linkTypes)
    {
        NodeTypes = nodeTypes;
        LinkTypes = linkTypes;
    }

    /// <summary>The type of a new node, unless another one is chosen.</summary>
    public static NodeType Generic { get; } = new("generic", "Generic");

    public static LinkType RelatesTo { get; } = new("relates-to", ["relates to", "related to"]);

    public static LinkType Contains { get; } = new("contains", ["contains"], ["contained by"]);

    public static LinkType IsA { get; } = new("is-a", ["is a", "is an"], ["has instance"]);

    /// <summary>A node with content: Markdown text, edited in its details.</summary>
    public static NodeType Note { get; } = new("note", "Note");

    /// <summary>A node with a file of any size, imported and exported from its details.</summary>
    public static NodeType Dataset { get; } = new("dataset", "Dataset");

    /// <summary>A way to look at the graph: a tree or a list (<see cref="ViewDefinition"/>).</summary>
    public static NodeType View { get; } = new("view", "View");

    public static TypeCatalog BuiltIn { get; } = new([Generic, Note, Dataset, View], [RelatesTo, Contains, IsA]);

    public IReadOnlyList<NodeType> NodeTypes { get; }

    public IReadOnlyList<LinkType> LinkTypes { get; }

    /// <summary>Every link type, each way it reads: what the creation screen offers.</summary>
    public IReadOnlyList<LinkVerb> Verbs => LinkTypes.SelectMany(type => type.Verbs).ToList();

    /// <summary>The node type with this id, or null when this version does not know it.</summary>
    public NodeType? FindNodeType(string id) => NodeTypes.FirstOrDefault(type => type.Id == id);

    /// <summary>The link type with this id, or null when this version does not know it.</summary>
    public LinkType? FindLinkType(string id) => LinkTypes.FirstOrDefault(type => type.Id == id);

    /// <summary>The name of a node type, or its id when this version does not know it.</summary>
    public string NodeTypeName(string id) => FindNodeType(id)?.Name ?? id;

    /// <summary>
    /// How a link reads from one of its nodes: "contains" from its <see cref="Link.From"/> node,
    /// "contained by" from its <see cref="Link.To"/> node. Its type's id when this version does not know it.
    /// </summary>
    public string LinkName(Link link, Guid seenFrom) =>
        FindLinkType(link.Type) is { } type
            ? new LinkVerb(type, IsReverse: !type.IsSymmetric && seenFrom != link.From).Name
            : link.Type;
}
