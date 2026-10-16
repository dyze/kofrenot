namespace Kofrenot.Core.Graph;

/// <summary>
/// A kind of link. <see cref="Phrases"/> are the words that name it in the input box, the first
/// one being its name ("relates to", then its aliases). A symmetric link reads the same both ways.
/// </summary>
public sealed record LinkType(string Id, IReadOnlyList<string> Phrases, bool IsSymmetric)
{
    public string Name => Phrases[0];
}
