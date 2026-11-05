namespace Kofrenot.Core.Graph;

/// <summary>
/// What a field's values hang on (<see cref="FieldSet"/>): a node, or a link (its qualifiers).
/// A link's are on its id, not on a way of reading it: "Dune written by Frank Herbert" and
/// "Frank Herbert wrote Dune" reach the same values.
/// </summary>
public readonly record struct FieldHolder(Guid Id, bool IsLink)
{
    public static FieldHolder OfNode(Guid node) => new(node, IsLink: false);

    public static FieldHolder OfLink(Guid link) => new(link, IsLink: true);

    public static implicit operator FieldHolder(Node node) => OfNode(node.Id);

    public static implicit operator FieldHolder(Link link) => OfLink(link.Id);
}
