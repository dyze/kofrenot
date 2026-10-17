namespace Kofrenot.Core.Graph;

/// <summary>
/// A kind of link. <see cref="Phrases"/> name it from its <see cref="Link.From"/> node ("contains"),
/// <see cref="ReversePhrases"/> from its <see cref="Link.To"/> node ("contained by"); the first
/// phrase of each is its name, the others its aliases. A symmetric link has no reverse phrases: it
/// reads the same both ways. Two link types are equal when their ids are: the same type with the
/// user's aliases added is still that type.
/// </summary>
public sealed record LinkType(string Id, IReadOnlyList<string> Phrases, IReadOnlyList<string>? ReversePhrases = null)
{
    public string Name => Phrases[0];

    public bool IsSymmetric => ReversePhrases is not { Count: > 0 };

    /// <summary>Read from its <see cref="Link.From"/> node.</summary>
    public LinkVerb Forward => new(this, IsReverse: false);

    /// <summary>Read from its <see cref="Link.To"/> node; null when it is symmetric.</summary>
    public LinkVerb? Reverse => IsSymmetric ? null : new(this, IsReverse: true);

    /// <summary>The ways to read it: forward, then reverse unless it is symmetric.</summary>
    public IEnumerable<LinkVerb> Verbs => Reverse is { } reverse ? [Forward, reverse] : [Forward];

    /// <summary>Its name: the generated one would loop, through <see cref="Forward"/> and back.</summary>
    public override string ToString() => Name;

    public bool Equals(LinkType? other) => other is not null && other.Id == Id;

    public override int GetHashCode() => Id.GetHashCode(StringComparison.Ordinal);
}
