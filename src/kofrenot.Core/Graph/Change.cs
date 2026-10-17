namespace Kofrenot.Core.Graph;

/// <summary>
/// One change of the vault, stored as one file named by <see cref="Id"/>. <see cref="At"/>
/// orders the changes when they are applied.
/// </summary>
public abstract record Change(Guid Id, DateTimeOffset At);

public sealed record NodeAdded(Guid Id, DateTimeOffset At, Node Node) : Change(Id, At);

public sealed record LinkAdded(Guid Id, DateTimeOffset At, Link Link) : Change(Id, At);
