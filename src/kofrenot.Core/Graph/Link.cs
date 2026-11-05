namespace Kofrenot.Core.Graph;

/// <summary>
/// A link between two nodes. <see cref="Type"/> is a link type id (<see cref="LinkType.Id"/>).
/// For a symmetric type, <see cref="From"/> and <see cref="To"/> are only the order it was typed in.
/// Links of one type between the same nodes are copies of one link (added on two devices before they
/// synced), unless <see cref="IsSeparate"/>: one added beside the others on purpose, with qualifiers
/// of its own (<i>married to Bob · 1995</i>, <i>married to Bob · 2015</i>).
/// </summary>
public sealed record Link(Guid Id, string Type, Guid From, Guid To, DateTimeOffset Created, bool IsSeparate = false)
{
    /// <summary>The node at the other end from <paramref name="node"/>.</summary>
    public Guid Other(Guid node) => node == From ? To : From;
}
