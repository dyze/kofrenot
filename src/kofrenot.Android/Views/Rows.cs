using Avalonia;
using Avalonia.Media;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;

namespace Kofrenot.Phone.Views;

/// <summary>A header between the rows of a list (Pinned, Recent).</summary>
public sealed record HeaderRow(string Text);

/// <summary>
/// A node in a list: its picture (<see cref="NodePicture"/>: its thumbnail, else its glyph and colour), its name
/// and, as on Windows, a detail only where it says more than its icon (a recent node's activity, the field a search
/// matched, how a link reads); on a node's page, the <see cref="Link"/> it is shown by.
/// </summary>
public sealed record NodeRow(Node Node, string Name, string Detail, string Glyph, IBrush? Color, Guid? Thumbnail = null, int Depth = 0, Link? Link = null)
{
    private const double IndentStep = 20;

    public bool HasColor => Color is not null;

    public bool HasDetail => Detail.Length > 0;

    public Thickness Indent => new(8 + Depth * IndentStep, 0, 8, 0);

    /// <summary>A node's row; no detail unless <paramref name="detail"/> is given.</summary>
    public static NodeRow Of(KnowledgeGraph graph, Node node, string? detail = null, string? mark = null, int depth = 0, Link? link = null)
    {
        var visual = NodeVisual.Of(graph, node);
        return new NodeRow(node, node.Name + mark, detail ?? string.Empty, NodeLook.GlyphOf(visual), NodeLook.Brush(visual.Color), visual.Thumbnail, depth, link);
    }
}

/// <summary>How a node is pictured on the phone without its thumbnail: a glyph for its icon, a dot of its colour.</summary>
public static class NodeLook
{
    private static readonly Dictionary<NodeColor, IBrush> Shades = new()
    {
        [NodeColor.Yellow] = new SolidColorBrush(Color.FromRgb(0xF2, 0xC1, 0x3A)),
        [NodeColor.Green] = new SolidColorBrush(Color.FromRgb(0x6F, 0xBF, 0x4A)),
        [NodeColor.Blue] = new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xE2)),
        [NodeColor.Pink] = new SolidColorBrush(Color.FromRgb(0xE2, 0x6A, 0x9A)),
        [NodeColor.Purple] = new SolidColorBrush(Color.FromRgb(0x8E, 0x7C, 0xE0)),
        [NodeColor.Teal] = new SolidColorBrush(Color.FromRgb(0x2B, 0xB5, 0x9A)),
        [NodeColor.Grey] = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
    };

    /// <summary>The colour's brush, as on Windows; null for none.</summary>
    public static IBrush? Brush(NodeColor color) => Shades.GetValueOrDefault(color);

    private static readonly Dictionary<string, string> TypeGlyphs = new()
    {
        ["Contact"] = "☺",
        ["People"] = "☻",
        ["Book"] = "▥",
        ["Home"] = "⌂",
        ["Place"] = "⌖",
        ["Calendar"] = "▦",
        ["Flag"] = "⚑",
        ["Tag"] = "#",
        ["Heart"] = "♥",
        ["Star"] = "★",
        ["Idea"] = "✦",
        ["Work"] = "▣",
        ["Music"] = "♫",
        ["Movie"] = "▶",
        ["Shop"] = "¤",
        ["Globe"] = "◍",
        ["Airplane"] = "✈",
        ["Game"] = "♟",
        ["Code"] = "{}",
    };

    /// <summary>The glyph of a node type's icon (<see cref="TypeIcons"/>); the generic one for a name this version does not know.</summary>
    public static string TypeGlyph(string icon) => TypeGlyphs.GetValueOrDefault(icon) ?? Glyph(NodeIcon.Generic);

    /// <summary>The glyph a node shows: its type's icon, or else its own.</summary>
    public static string GlyphOf(NodeVisual visual) => visual.TypeIcon is { } icon ? TypeGlyph(icon) : Glyph(visual.Icon);

    public static string Glyph(NodeIcon icon) => icon switch
    {
        NodeIcon.Note => "≡",
        NodeIcon.File => "▭",
        NodeIcon.Image => "▨",
        NodeIcon.Text => "¶",
        NodeIcon.Code => "{}",
        NodeIcon.Audio => "♪",
        NodeIcon.Video => "▶",
        NodeIcon.Archive => "▤",
        NodeIcon.Pdf => "▯",
        NodeIcon.View => "⊞",
        _ => "●",
    };
}
