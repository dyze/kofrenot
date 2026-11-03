using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>A node's card in a graph view: its picture and name, then the values of the fields the view names.</summary>
internal static class GraphCard
{
    private const double NameFontSize = 15;
    private const double LineFontSize = 12;

    public static Border Of(KnowledgeGraph graph, Node node, IReadOnlyList<string> lines, bool isSelected)
    {
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = node.Name, FontSize = NameFontSize, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        foreach (var line in lines)
        {
            var value = new TextBlock { Text = line, FontSize = LineFontSize, Opacity = 0.75, TextTrimming = TextTrimming.CharacterEllipsis };
            text.Children.Add(value);
        }

        var content = new DockPanel { LastChildFill = true };
        var picture = new NodeVisualView { Width = 28, Height = 28, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, Visual = NodeVisual.Of(graph, node) };
        DockPanel.SetDock(picture, Dock.Left);
        content.Children.Add(picture);
        content.Children.Add(text);

        var card = new Border
        {
            Width = GraphLayout.CardWidth,
            Height = GraphLayout.CardHeight,
            Padding = new Thickness(10, 6, 10, 6),
            CornerRadius = new CornerRadius(8),
            Child = content,
            Tag = node,
            ToolTip = lines.Count > 0 ? string.Join('\n', lines.Prepend(node.Name)) : node.Name,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        card.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        Mark(card, isSelected);
        return card;
    }

    /// <summary>The ring of a card: the accent colour when selected, a thin grey line otherwise.</summary>
    public static void Mark(Border card, bool isSelected)
    {
        card.BorderThickness = new Thickness(isSelected ? 2 : 1);
        card.SetResourceReference(Border.BorderBrushProperty, isSelected ? "AccentFillColorDefaultBrush" : "ControlStrongStrokeColorDefaultBrush");
    }
}
