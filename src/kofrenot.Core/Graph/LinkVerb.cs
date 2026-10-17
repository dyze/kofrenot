namespace Kofrenot.Core.Graph;

/// <summary>
/// A link type read one way: forward, from its <see cref="Link.From"/> node ("Dune contains
/// Arrakis"), or reverse, from its <see cref="Link.To"/> node ("Arrakis contained by Dune").
/// </summary>
public sealed record LinkVerb(LinkType Type, bool IsReverse)
{
    /// <summary>The phrases typed for this way, its name first.</summary>
    public IReadOnlyList<string> Phrases => IsReverse ? Type.ReversePhrases! : Type.Phrases;

    public string Name => Phrases[0];
}
