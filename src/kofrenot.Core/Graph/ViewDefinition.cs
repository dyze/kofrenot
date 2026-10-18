namespace Kofrenot.Core.Graph;

/// <summary>What a view node shows: a way to look at the graph, kept in its <see cref="ViewEdited"/> versions.</summary>
public abstract record ViewDefinition;

/// <summary>A tree from <see cref="Root"/>, each node holding the nodes its links of <see cref="LinkType"/> lead to.</summary>
public sealed record TreeDefinition(Guid Root, string LinkType) : ViewDefinition;

/// <summary>A list of every node of <see cref="NodeType"/>.</summary>
public sealed record FlatDefinition(string NodeType) : ViewDefinition;
