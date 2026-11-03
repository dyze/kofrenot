namespace Kofrenot.Core.Graph;

/// <summary>What a view node shows: a way to look at the graph, kept in its <see cref="ViewEdited"/> versions.</summary>
public abstract record ViewDefinition;

/// <summary>A tree from <see cref="Root"/>, each node holding the nodes its links of <see cref="LinkType"/> lead to.</summary>
public sealed record TreeDefinition(Guid Root, string LinkType) : ViewDefinition;

/// <summary>A list of every node of <see cref="NodeType"/>.</summary>
public sealed record FlatDefinition(string NodeType) : ViewDefinition;

/// <summary>How a graph view places the nodes a link type joins.</summary>
public enum GraphRole
{
    /// <summary>From a node one rank above to one below: parent of, contains.</summary>
    Down,

    /// <summary>From a node one rank below to one above: child of, is in.</summary>
    Up,

    /// <summary>Both nodes on the same rank, side by side: married to.</summary>
    Beside,

    /// <summary>Drawn, with no rank between them.</summary>
    Free,
}

/// <summary>How a graph view lays its nodes out, until they are moved by hand.</summary>
public enum GraphLayoutKind
{
    /// <summary>In ranks, from the links whose role is <see cref="GraphRole.Down"/> or <see cref="GraphRole.Up"/>: a family tree, an org chart.</summary>
    Layered,

    /// <summary>Linked nodes pulled together, the others pushed apart: the relations around a node.</summary>
    Network,
}

/// <summary>A link type a graph view follows, and how it places the nodes it joins.</summary>
public sealed record GraphLink(string LinkType, GraphRole Role);

/// <summary>
/// A graph from <see cref="Root"/>: the nodes reached within <see cref="Depth"/> steps through
/// <see cref="Links"/>, either way, of <see cref="NodeTypes"/> only (any type when empty), laid out
/// by <see cref="Layout"/>. Each node's card shows its values of the fields named in <see cref="Fields"/>.
/// </summary>
public sealed record GraphDefinition(
    Guid Root,
    int Depth,
    GraphLayoutKind Layout,
    IReadOnlyList<GraphLink> Links,
    IReadOnlyList<string> NodeTypes,
    IReadOnlyList<string> Fields) : ViewDefinition
{
    public const int MinDepth = 1;
    public const int MaxDepth = 10;

    // By value, lists included: an edit that changes nothing saves nothing (ViewPlanner.Save).
    public bool Equals(GraphDefinition? other) =>
        other is not null
        && (Root, Depth, Layout) == (other.Root, other.Depth, other.Layout)
        && Links.SequenceEqual(other.Links)
        && NodeTypes.SequenceEqual(other.NodeTypes)
        && Fields.SequenceEqual(other.Fields);

    public override int GetHashCode() => HashCode.Combine(Root, Depth, Layout, Links.Count, NodeTypes.Count, Fields.Count);
}
