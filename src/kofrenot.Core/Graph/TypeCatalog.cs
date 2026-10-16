namespace Kofrenot.Core.Graph;

/// <summary>
/// The node and link types known to this version. Built in for now; user-defined types will be
/// added to it later.
/// </summary>
public sealed class TypeCatalog
{
    public TypeCatalog(IReadOnlyList<LinkType> linkTypes) => LinkTypes = linkTypes;

    /// <summary>The type of every node added in 0.1.</summary>
    public static NodeType Generic { get; } = new("generic", "Generic");

    public static LinkType RelatesTo { get; } = new("relates-to", ["relates to", "related to"], IsSymmetric: true);

    public static TypeCatalog BuiltIn { get; } = new([RelatesTo]);

    public IReadOnlyList<LinkType> LinkTypes { get; }

    /// <summary>The link type with this id, or null when this version does not know it.</summary>
    public LinkType? FindLinkType(string id) => LinkTypes.FirstOrDefault(type => type.Id == id);
}
