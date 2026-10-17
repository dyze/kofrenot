namespace Kofrenot.Core.Graph;

/// <summary>
/// A name a node has: the name it was added with (<see cref="Id"/> is then the node's id), or a
/// rename (<see cref="Id"/> is the change's).
/// </summary>
public sealed record NameVersion(Guid Id, string Name, DateTimeOffset At);
