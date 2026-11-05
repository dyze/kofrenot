namespace Kofrenot.Core.Fields;

/// <summary>
/// What a field holds. The small kinds (<see cref="FieldKinds.IsSmall"/>) are metadata: written in
/// the change, always local, searched offline. An <see cref="Image"/> is a picture: described in its
/// change, its file and thumbnail stored as a dataset version's are. <see cref="Markdown"/>,
/// <see cref="File"/> and <see cref="View"/> are the content of the built-in types (a note's text, a
/// dataset's file, a view's definition), written by their own changes. <see cref="Unknown"/> is a
/// kind written by a newer kofrenot: its value is shown as text, never changed.
/// </summary>
public enum FieldKind
{
    Text,
    Number,
    Date,
    Url,
    Email,
    Address,
    Checkbox,
    Choice,
    Image,
    Markdown,
    File,
    View,
    Unknown,
}

/// <summary>How a kind is written in the vault ("text"), and what it is for.</summary>
public static class FieldKinds
{
    /// <summary>The kinds whose value is in the change: typed, chosen or ticked.</summary>
    public static IReadOnlyList<FieldKind> Small { get; } =
        [FieldKind.Text, FieldKind.Number, FieldKind.Date, FieldKind.Url, FieldKind.Email, FieldKind.Address, FieldKind.Checkbox, FieldKind.Choice];

    /// <summary>The kinds a field of a type of the user's can have: the small ones and an image.</summary>
    public static IReadOnlyList<FieldKind> OfTypes { get; } = [.. Small, FieldKind.Image];

    /// <summary>Whether the kind is metadata, with its value in the change.</summary>
    public static bool IsSmall(this FieldKind kind) => kind <= FieldKind.Choice;

    /// <summary>Whether the kind is a built-in type's content (a note's text, a dataset's file, a view's definition), shown apart.</summary>
    public static bool IsContent(this FieldKind kind) => kind is FieldKind.Markdown or FieldKind.File or FieldKind.View;

    /// <summary>The name written for <paramref name="kind"/>: "text", "checkbox".</summary>
    public static string NameOf(FieldKind kind) => kind.ToString().ToLowerInvariant();

    /// <summary>
    /// The kind of a name a type's field can have (<see cref="OfTypes"/>); null for a content kind, or
    /// for a name this version does not know, then kept as <see cref="FieldKind.Unknown"/> when <paramref name="unknown"/>.
    /// </summary>
    public static FieldKind? Parse(string? name, bool unknown = false) =>
        OfTypes.Cast<FieldKind?>().FirstOrDefault(kind => NameOf(kind!.Value) == name)
        ?? (unknown && !string.IsNullOrWhiteSpace(name) && !IsContentName(name) ? FieldKind.Unknown : null);

    private static bool IsContentName(string name) =>
        Enum.GetValues<FieldKind>().Any(kind => kind.IsContent() && NameOf(kind) == name);
}
