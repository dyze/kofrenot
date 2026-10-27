namespace Kofrenot.Core.Fields;

/// <summary>
/// A field of a node type: <see cref="Id"/> stays when it is renamed, so its values follow it.
/// <see cref="Choices"/> are a choice's options, in order; empty for the other kinds.
/// </summary>
public sealed record FieldDefinition(string Id, string Name, FieldKind Kind, IReadOnlyList<string> Choices)
{
    public FieldDefinition(string id, string name, FieldKind kind)
        : this(id, name, kind, [])
    {
    }

    public bool Equals(FieldDefinition? other) =>
        other is not null && (other.Id, other.Name, other.Kind) == (Id, Name, Kind) && other.Choices.SequenceEqual(Choices);

    public override int GetHashCode() => HashCode.Combine(Id, Name, Kind, Choices.Count);
}
