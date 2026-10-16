namespace Kofrenot.Core.Graph;

/// <summary>
/// A link between two nodes. <see cref="Type"/> is a link type id (<see cref="LinkType.Id"/>).
/// For a symmetric type, <see cref="From"/> and <see cref="To"/> are only the order it was typed in.
/// </summary>
public sealed record Link(Guid Id, string Type, Guid From, Guid To, DateTimeOffset Created)
{
    /// <summary>The node at the other end from <paramref name="node"/>.</summary>
    public Guid Other(Guid node) => node == From ? To : From;
}
