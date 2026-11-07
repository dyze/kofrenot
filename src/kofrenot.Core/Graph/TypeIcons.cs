namespace Kofrenot.Core.Graph;

/// <summary>
/// The icons a node type of the user's can have, by name (<see cref="NodeTypeDefined.Icon"/>): a
/// fixed set each app draws in its own way. A name this version does not know shows no icon.
/// </summary>
public static class TypeIcons
{
    public static IReadOnlyList<string> All { get; } =
    [
        "Contact", "People", "Doctor", "Gender", "Gender-female", "Gender-male", "Gender-neutral",
        "Book", "Bookmark", "Home", "Place", "Calendar", "Birthday-cake", "Gift", "Flag", "Tag", "Heart", "Star",
        "Idea", "Work", "Tool", "Chart", "Music", "Movie", "Shop", "Card", "Package", "Cook", "Pills",
        "Dog", "Flower", "Tree", "Globe", "Airplane", "Car", "Bike", "Motorbike",
        "Phone", "Device", "Chat", "Cloud", "Game", "Code", "Bug",
    ];

    /// <summary>The icon of this name, or null when this version does not know it.</summary>
    public static string? Find(string? name) => All.FirstOrDefault(icon => icon == name);

    /// <summary>How an icon's name is shown: <c>Birthday-cake</c> as "Birthday cake".</summary>
    public static string LabelOf(string icon) => icon.Replace('-', ' ');
}
