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

    public static LinkType RelatesTo { get; } = new("relates-to", ["relates to", "related to"], IsSymmetric: true);

    public static TypeCatalog BuiltIn { get; } = new([Generic], [RelatesTo]);

    public IReadOnlyList<NodeType> NodeTypes { get; }

    public IReadOnlyList<LinkType> LinkTypes { get; }

    /// <summary>The node type with this id, or null when this version does not know it.</summary>
    public NodeType? FindNodeType(string id) => NodeTypes.FirstOrDefault(type => type.Id == id);

    /// <summary>The link type with this id, or null when this version does not know it.</summary>
    public LinkType? FindLinkType(string id) => LinkTypes.FirstOrDefault(type => type.Id == id);

    /// <summary>The name of a node type, or its id when this version does not know it.</summary>
    public string NodeTypeName(string id) => FindNodeType(id)?.Name ?? id;

    /// <summary>The name of a link type, or its id when this version does not know it.</summary>
    public string LinkTypeName(string id) => FindLinkType(id)?.Name ?? id;
}
