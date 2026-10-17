using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Browse;

/// <summary>What the details screen shows of a node: its properties, then its links.</summary>
/// <param name="Name">The name, marked "¹", "²", ... among homonyms.</param>
/// <param name="Type">The type's name, or its id when this version does not know it.</param>
/// <param name="NameConflict">The names given on two devices before they synced, oldest first; empty without a conflict.</param>
public sealed record NodeDetails(Node Node, string Name, string Type, IReadOnlyList<LinkRow> Links, IReadOnlyList<string> NameConflict);

/// <summary>
/// A link of the node shown: its type and the node at the other end. <see cref="CanOpen"/>: that
/// node can be opened one level deeper (not at the last level, and not already in the trail).
/// </summary>
public sealed record LinkRow(string Type, string Other, Node OtherNode, bool CanOpen, bool IsSelected);
