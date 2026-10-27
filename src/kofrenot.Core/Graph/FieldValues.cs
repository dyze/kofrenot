namespace Kofrenot.Core.Graph;

/// <summary>
/// The current values of one field of a node (<see cref="Owner"/> and <see cref="Field"/>, as in
/// <see cref="FieldSet"/>), oldest first: one normally, several when it was set on two devices
/// before they synced (a conflict). A value cleared is a version with no value.
/// </summary>
public sealed record FieldVersions(string Owner, string Field, IReadOnlyList<FieldSet> Current)
{
    /// <summary>The version shown: the latest current one.</summary>
    public FieldSet Shown => Current[^1];

    public bool IsConflict => Current.Count > 1;
}

/// <summary>The values of the fields of every node (<see cref="FieldSet"/>), and each field's current ones.</summary>
internal sealed class FieldValues
{
    private readonly NodeVersions<FieldSet> _versions = new();

    /// <summary>False when this value is already there.</summary>
    public bool Add(FieldSet value) => _versions.Add(value);

    /// <summary>The values of a node, whatever they replace: when it last changed.</summary>
    public IEnumerable<FieldSet> Of(Guid node) => _versions.Of(node);

    /// <summary>The fields of a node that were set, each with its current values, in the order they were first set.</summary>
    public IReadOnlyList<FieldVersions> Current(Guid node) =>
        _versions.Of(node)
            .GroupBy(value => (value.Owner, value.Field))
            .Select(values => (First: values.Min(value => value.At), Field: new FieldVersions(values.Key.Owner, values.Key.Field, CurrentVersions.Of(values))))
            .OrderBy(field => field.First)
            .ThenBy(field => field.Field.Owner, StringComparer.Ordinal)
            .ThenBy(field => field.Field.Field, StringComparer.Ordinal)
            .Select(field => field.Field)
            .ToList();
}
