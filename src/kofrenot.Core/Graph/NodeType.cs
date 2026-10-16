namespace Kofrenot.Core.Graph;

/// <summary>A kind of node. <see cref="Id"/> is what the database stores.</summary>
public sealed record NodeType(string Id, string Name);
