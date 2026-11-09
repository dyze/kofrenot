using Avalonia;
using Avalonia.Media;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;

namespace Kofrenot.Phone.Views;

/// <summary>A header between the rows of a list (Pinned, Recent).</summary>
public sealed record HeaderRow(string Text);

/// <summary>After <c>?</c> alone, a way to start a query: a tap puts its text in the search box (<see cref="Kofrenot.Core.Input.QueryStart"/>).</summary>
public sealed record QueryStartRow(string Text, string Detail, string Insert);

/// <summary>
/// A node in a list: its picture (<see cref="NodePicture"/>: its thumbnail, else its icon and colour), its name
/// and, as on Windows, a detail only where it says more than its icon (a recent node's activity, the field a search
/// matched, how a link reads); on a node's page, the <see cref="Link"/> it is shown by.
/// </summary>
public sealed record NodeRow(Node Node, string Name, string Detail, string Icon, IBrush? Color, Guid? Thumbnail = null, int Depth = 0, Link? Link = null)
{
    /// <summary>False for a link to a node open on a screen before (<see cref="NodeBrowser.Before"/>).</summary>
    public bool CanOpen { get; init; } = true;

    private const double IndentStep = 20;

    public bool HasColor => Color is not null;

    public bool HasDetail => Detail.Length > 0;

    public Thickness Indent => new(8 + Depth * IndentStep, 0, 8, 0);

    /// <summary>A node's row; no detail unless <paramref name="detail"/> is given.</summary>
    public static NodeRow Of(KnowledgeGraph graph, Node node, string? detail = null, string? mark = null, int depth = 0, Link? link = null)
    {
        var visual = NodeVisual.Of(graph, node);
        return new NodeRow(node, node.Name + mark, detail ?? string.Empty, IconPaths.NameOf(visual), NodeLook.Brush(visual.Color), visual.Thumbnail, depth, link);
    }
}

/// <summary>How a node is pictured on the phone without its thumbnail: its icon (<see cref="IconPaths"/>), a dot of its colour.</summary>
public static class NodeLook
{
    private static readonly Dictionary<NodeColor, IBrush> Shades = new()
    {
        [NodeColor.Red] = new SolidColorBrush(Color.FromRgb(0xE0, 0x52, 0x4A)),
        [NodeColor.Orange] = new SolidColorBrush(Color.FromRgb(0xF0, 0x8A, 0x3C)),
        [NodeColor.Yellow] = new SolidColorBrush(Color.FromRgb(0xF2, 0xC1, 0x3A)),
        [NodeColor.Green] = new SolidColorBrush(Color.FromRgb(0x6F, 0xBF, 0x4A)),
        [NodeColor.Teal] = new SolidColorBrush(Color.FromRgb(0x2B, 0xB5, 0x9A)),
        [NodeColor.Blue] = new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xE2)),
        [NodeColor.Purple] = new SolidColorBrush(Color.FromRgb(0x8E, 0x7C, 0xE0)),
        [NodeColor.Pink] = new SolidColorBrush(Color.FromRgb(0xE2, 0x6A, 0x9A)),
        [NodeColor.Brown] = new SolidColorBrush(Color.FromRgb(0xA0, 0x78, 0x5A)),
        [NodeColor.Grey] = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)),
    };

    /// <summary>The colour's brush, as on Windows; null for none.</summary>
    public static IBrush? Brush(NodeColor color) => Shades.GetValueOrDefault(color);
}
