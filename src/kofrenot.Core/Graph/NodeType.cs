using Kofrenot.Core.Fields;

namespace Kofrenot.Core.Graph;

/// <summary>
/// A kind of node: a name and a list of fields, with an icon (a name the apps know, or none) and a
/// colour. <see cref="Id"/> is what the vault stores. Two node types are equal when their ids are:
/// a type renamed is still that type.
/// </summary>
public sealed record NodeType(string Id, string Name, IReadOnlyList<FieldDefinition> Fields, string? Icon = null, NodeColor Color = NodeColor.None)
{
    public NodeType(string id, string name)
        : this(id, name, [])
    {
    }

    /// <summary>Whether its nodes have content (a note's text, a dataset's file, a view's definition): a node has one such type at most.</summary>
    public bool HasContent => Fields.Any(definition => !definition.Kind.IsSmall());

    public bool Equals(NodeType? other) => other is not null && other.Id == Id;

    public override int GetHashCode() => Id.GetHashCode(StringComparison.Ordinal);
}
