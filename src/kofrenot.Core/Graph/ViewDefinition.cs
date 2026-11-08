namespace Kofrenot.Core.Graph;

/// <summary>What a view node shows: a way to look at the graph, kept in its <see cref="ViewEdited"/> versions.</summary>
public abstract record ViewDefinition;

/// <summary>
/// A tree from <see cref="Root"/>, each node holding the nodes its links of <see cref="LinkType"/> lead to: by
/// the value of the link's qualifier <see cref="Order"/> (a field id of the link type), or by name when null.
/// </summary>
public sealed record TreeDefinition(Guid Root, string LinkType, string? Order = null) : ViewDefinition;

/// <summary>A list of every node of <see cref="NodeType"/>.</summary>
public sealed record FlatDefinition(string NodeType) : ViewDefinition;

/// <summary>
/// The nodes a query finds (<c>?book: read by Alice, Date 2026</c>): those of <see cref="NodeType"/> (any type when null)
/// that read <see cref="LinkType"/> (<see cref="Reverse"/>: from its other end) to <see cref="Node"/> (any node when null),
/// through a link whose qualifiers match <see cref="Qualifiers"/>. Each row shows its <see cref="Columns"/>: the nodes its
/// links lead to (<c>written by</c> → <i>Frank Herbert</i>), a field of its node, or a qualifier of the link found.
/// </summary>
public sealed record QueryDefinition(
    string? NodeType,
    string LinkType,
    bool Reverse,
    Guid? Node,
    IReadOnlyList<QualifierFilter> Qualifiers,
    IReadOnlyList<ViewColumn> Columns) : ViewDefinition
{
    // By value, lists included: an edit that changes nothing saves nothing (ViewPlanner.Save).
    public bool Equals(QueryDefinition? other) =>
        other is not null
        && (NodeType, LinkType, Reverse, Node) == (other.NodeType, other.LinkType, other.Reverse, other.Node)
        && Qualifiers.SequenceEqual(other.Qualifiers)
        && Columns.SequenceEqual(other.Columns);

    public override int GetHashCode() => HashCode.Combine(NodeType, LinkType, Reverse, Node, Qualifiers.Count, Columns.Count);
}

/// <summary>
/// A qualifier a link must have (<see cref="Field"/>, a field id of its link type), with a value matching <see cref="Value"/>
/// as <see cref="Input.ValueQuery"/> says (<c>2026</c>, <c>2026-01..2026-06</c>); any value when empty.
/// </summary>
public sealed record QualifierFilter(string Field, string Value);

/// <summary>A column of a query view: what each row shows beside its node's name.</summary>
public abstract record ViewColumn;

/// <summary>The nodes a row's links of <see cref="LinkType"/> lead to, read from its other end when <see cref="Reverse"/>.</summary>
public sealed record LinkColumn(string LinkType, bool Reverse) : ViewColumn;

/// <summary>The value of a field of the node found: <see cref="Field"/>, a field id of its node type <see cref="Owner"/>.</summary>
public sealed record FieldColumn(string Owner, string Field) : ViewColumn;

/// <summary>The values of a qualifier (<see cref="Field"/>, a field id of the query's link type) on the links the query found the node through.</summary>
public sealed record QualifierColumn(string Field) : ViewColumn;

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
