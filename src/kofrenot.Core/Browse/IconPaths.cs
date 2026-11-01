using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Browse;

/// <summary>
/// The icons of both apps, by name (<c>delete</c>, <c>note</c>, <c>contact</c>…): the SVGs of <c>Icons/</c>,
/// each a 24×24 view box and one filled path, read once. Each app draws <see cref="PathOf"/> as its own geometry.
/// </summary>
public static class IconPaths
{
    public const string Fallback = "generic";

    private const string Prefix = "icons/";
    private const string DataStart = " d=\"";

    private static readonly Lazy<IReadOnlyDictionary<string, string>> Paths = new(Read);

    /// <summary>Every icon's name.</summary>
    public static IEnumerable<string> Names => Paths.Value.Keys;

    /// <summary>The path data of an icon; the <see cref="Fallback"/>'s for a name not known.</summary>
    public static string PathOf(string name) => Paths.Value.GetValueOrDefault(name) ?? Paths.Value[Fallback];

    /// <summary>The icon of what a node holds.</summary>
    public static string NameOf(NodeIcon icon) => icon.ToString().ToLowerInvariant();

    /// <summary>The icon of a node type's icon (<see cref="TypeIcons"/>, <c>Contact</c> → <c>contact</c>); null for none, or one this version does not know.</summary>
    public static string? NameOfType(string? icon) => TypeIcons.Find(icon)?.ToLowerInvariant();

    /// <summary>The icon a node shows: its type's, else that of what it holds.</summary>
    public static string NameOf(NodeVisual visual) => NameOfType(visual.TypeIcon) ?? NameOf(visual.Icon);

    /// <summary>The path data of one SVG (its <c>d="…"</c>); null when it has none.</summary>
    public static string? PathIn(string svg)
    {
        var start = svg.IndexOf(DataStart, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += DataStart.Length;
        var end = svg.IndexOf('"', start);
        return end > start ? svg[start..end] : null;
    }

    private static Dictionary<string, string> Read()
    {
        var assembly = typeof(IconPaths).Assembly;
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var resource in assembly.GetManifestResourceNames().Where(name => name.StartsWith(Prefix, StringComparison.Ordinal)))
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
            if (PathIn(reader.ReadToEnd()) is { } path)
            {
                paths[Path.GetFileNameWithoutExtension(resource[Prefix.Length..])] = path;
            }
        }

        return paths;
    }
}
