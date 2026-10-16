namespace Kofrenot.Core.Graph;

/// <summary>A node of the graph. <see cref="Type"/> is a node type id (<see cref="NodeType.Id"/>).</summary>
public sealed record Node(Guid Id, string Type, string Name, DateTimeOffset Created);
