namespace Kofrenot.Core.Graph;

/// <summary>A key of a record (a dotted path into nested objects: <c>publisher.name</c>) read for a field of the mapping's node type (its id).</summary>
public sealed record FieldRule(string Key, string Field);

/// <summary>
/// A key of a record whose values are names of nodes of <see cref="NodeType"/>, each linked to the
/// imported node by <see cref="LinkType"/>: from the imported node, or from the named one when
/// <see cref="IsReverse"/> (<i>Frank Herbert wrote Dune</i>).
/// </summary>
public sealed record LinkRule(string Key, string NodeType, string LinkType, bool IsReverse);

/// <summary>
/// How a mapping node imports records (<see cref="MappingEdited"/>): from <see cref="Source"/>, as
/// nodes of <see cref="NodeType"/>, named by the first of <see cref="NameKeys"/> a record has. A
/// record finds its node by <see cref="Match"/> (a field id of the type, or none), then by its names,
/// compared with the nodes' names and their <see cref="OtherNames"/> fields.
/// </summary>
public sealed record MappingDefinition(
    string Source,
    string NodeType,
    IReadOnlyList<string> NameKeys,
    string? Match,
    IReadOnlyList<string> OtherNames,
    IReadOnlyList<FieldRule> Fields,
    IReadOnlyList<LinkRule> Links)
{
    /// <summary>A <c>.json</c> file holding an array of objects: the only source.</summary>
    public const string Json = "json";

    // By value, lists included: an edit that changes nothing saves nothing (MappingPlanner.Save).
    public bool Equals(MappingDefinition? other) =>
        other is not null
        && (Source, NodeType, Match) == (other.Source, other.NodeType, other.Match)
        && NameKeys.SequenceEqual(other.NameKeys)
        && OtherNames.SequenceEqual(other.OtherNames)
        && Fields.SequenceEqual(other.Fields)
        && Links.SequenceEqual(other.Links);

    public override int GetHashCode() => HashCode.Combine(Source, NodeType, Match, NameKeys.Count, Fields.Count, Links.Count);
}
