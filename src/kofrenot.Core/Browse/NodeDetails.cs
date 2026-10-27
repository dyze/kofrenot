using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Browse;

/// <summary>What the details screen shows of a node: its properties, then its links.</summary>
/// <param name="Name">The name, marked "¹", "²", ... among homonyms.</param>
/// <param name="Type">The names of its types (<see cref="Types"/>), "Person, Author".</param>
/// <param name="NameConflict">The names given on two devices before they synced, oldest first; empty without a conflict.</param>
/// <param name="IsPinned">Listed while the input box is empty.</param>
/// <param name="Types">Its types, oldest first, a type this version does not know named by its id; <see cref="TypeCatalog.Generic"/> when it has none.</param>
/// <param name="Color">The colour shown: the latest current one.</param>
/// <param name="ColorConflict">The colours given on two devices before they synced, oldest first; empty without a conflict.</param>
/// <param name="Fields">Its fields (<see cref="NodeFields"/>).</param>
public sealed record NodeDetails(
    Node Node,
    string Name,
    string Type,
    IReadOnlyList<LinkRow> Links,
    IReadOnlyList<string> NameConflict,
    bool IsPinned,
    IReadOnlyList<NodeType> Types,
    NodeColor Color,
    IReadOnlyList<NodeColor> ColorConflict,
    IReadOnlyList<NodeField> Fields);

/// <summary>
/// A link of the node shown: its type, and the node at the other end with its picture. <see cref="CanOpen"/>: that
/// node can be opened one level deeper (not at the last level, and not already in the trail).
/// </summary>
public sealed record LinkRow(string Type, string Other, Node OtherNode, bool CanOpen, bool IsSelected, NodeVisual Visual, Link Link);
