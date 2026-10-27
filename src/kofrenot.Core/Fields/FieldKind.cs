namespace Kofrenot.Core.Fields;

/// <summary>
/// What a field holds. The small kinds (<see cref="IsSmall"/>) are metadata: written in the change,
/// always local, searched offline. <see cref="Markdown"/>, <see cref="File"/> and <see cref="View"/>
/// are the content of the built-in types (a note's text, a dataset's file, a view's definition),
/// written by their own changes.
/// </summary>
public enum FieldKind
{
    Text,
    Number,
    Date,
    Url,
    Checkbox,
    Choice,
    Markdown,
    File,
    View,
}

/// <summary>How a kind is written in the vault ("text"), and what it is for.</summary>
public static class FieldKinds
{
    /// <summary>The kinds a type of the user's, or a node's own field, can have.</summary>
    public static IReadOnlyList<FieldKind> Small { get; } =
        [FieldKind.Text, FieldKind.Number, FieldKind.Date, FieldKind.Url, FieldKind.Checkbox, FieldKind.Choice];

    /// <summary>Whether the kind is metadata, with its value in the change.</summary>
    public static bool IsSmall(this FieldKind kind) => kind <= FieldKind.Choice;

    /// <summary>The name written for <paramref name="kind"/>: "text", "checkbox".</summary>
    public static string NameOf(FieldKind kind) => kind.ToString().ToLowerInvariant();

    /// <summary>The small kind of a name; null for a name this version does not know, or a content kind.</summary>
    public static FieldKind? Parse(string? name) =>
        Small.Cast<FieldKind?>().FirstOrDefault(kind => NameOf(kind!.Value) == name);
}
