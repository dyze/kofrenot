using Kofrenot.Core.Browse;

namespace Kofrenot.Core.Input;

/// <summary>
/// A part of the input box shown as a label: a node's name, or the link type typed
/// (<see cref="IsLinkType"/>). A node's label <see cref="IsNew"/> when no node has its name;
/// <see cref="Homonyms"/> counts the nodes that have it, <see cref="Visual"/> pictures the one used.
/// </summary>
public sealed record InputChip(string Text, bool IsLinkType, bool IsNew = false, int Homonyms = 0, NodeVisual? Visual = null)
{
    /// <summary>Shown after the name when several nodes have it: "2", "3", ...; empty otherwise.</summary>
    public string HomonymMark => Homonyms > 1 ? Homonyms.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
}

/// <summary>
/// What Tab would add after the text (<see cref="InputCompletion"/>): <see cref="Rest"/> is shown in
/// grey, <see cref="Label"/> becomes a label. A link type comes after the first <see cref="WordsBefore"/> words.
/// </summary>
public sealed record Proposal(string Rest, string Label, bool IsLinkType, int WordsBefore = 0);
