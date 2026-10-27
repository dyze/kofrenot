using Kofrenot.Core.Graph;

namespace Kofrenot.UI.Views;

/// <summary>The Segoe Fluent Icons glyph of each icon a node type of the user's can have (<see cref="TypeIcons"/>).</summary>
public static class TypeGlyphs
{
    private static readonly Dictionary<string, string> Glyphs = new()
    {
        ["Contact"] = "",
        ["People"] = "",
        ["Book"] = "",
        ["Home"] = "",
        ["Place"] = "",
        ["Calendar"] = "",
        ["Flag"] = "",
        ["Tag"] = "",
        ["Heart"] = "",
        ["Star"] = "",
        ["Idea"] = "",
        ["Work"] = "",
        ["Music"] = "",
        ["Movie"] = "",
        ["Shop"] = "",
        ["Globe"] = "",
        ["Airplane"] = "",
        ["Game"] = "",
        ["Code"] = "",
    };

    /// <summary>The glyph of an icon's name; empty for none, or a name this version does not know.</summary>
    public static string Of(string? icon) => icon is not null && Glyphs.TryGetValue(icon, out var glyph) ? glyph : string.Empty;
}
