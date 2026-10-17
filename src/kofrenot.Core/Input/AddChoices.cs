using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// What the user picked for a request: for each side, which of the nodes with its name (0: the
/// oldest), and the type of the node added when it is new (null: <see cref="TypeCatalog.Generic"/>).
/// A node request only uses the left side.
/// </summary>
public sealed record AddChoices(int Left = 0, int Right = 0, NodeType? LeftType = null, NodeType? RightType = null)
{
    public static AddChoices Default { get; } = new();
}
