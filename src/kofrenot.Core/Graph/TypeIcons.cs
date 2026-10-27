namespace Kofrenot.Core.Graph;

/// <summary>
/// The icons a node type of the user's can have, by name (<see cref="NodeTypeDefined.Icon"/>): a
/// fixed set each app draws in its own way. A name this version does not know shows no icon.
/// </summary>
public static class TypeIcons
{
    public static IReadOnlyList<string> All { get; } =
    [
        "Contact", "People", "Book", "Home", "Place", "Calendar", "Flag", "Tag", "Heart", "Star",
        "Idea", "Work", "Music", "Movie", "Shop", "Globe", "Airplane", "Game", "Code",
    ];

    /// <summary>The icon of this name, or null when this version does not know it.</summary>
    public static string? Find(string? name) => All.FirstOrDefault(icon => icon == name);
}
