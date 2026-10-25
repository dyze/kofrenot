using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;

namespace Kofrenot.UI.Views;

/// <summary>
/// Cards (a toggle on a list's bar, kept per view in <see cref="ViewStates"/>): the list's items
/// wrap as cards of one size, each its node's picture and name over a preview (<see cref="NodeCard"/>),
/// in its colour. A card's preview is read once it comes into view, and dropped at the next redraw.
/// </summary>
public partial class ViewPanel
{
    private const double CardWidth = 220;
    private const double CardHeight = 170;

    // The cards whose preview is not read yet, each with the place it goes.
    private readonly List<(ListBoxItem Item, Node Node, Decorator Body)> _unreadCards = [];
    private ItemsPanelTemplate? _rowsPanel;

    /// <summary>Reads a note version's content for its card; null when its file is not there.</summary>
    public Func<Guid, string?>? ReadNote { get; set; }

    /// <summary>Where a note version's content stands while it is not here.</summary>
    public Func<Guid, ContentState> ContentStateOf { get; set; } = _ => ContentState.NotHere;

    private bool ShowsCards => _view is not null && States?.ShowsCards(_vault, _view.Id) == true;

    private void Cards_Click(object sender, RoutedEventArgs e)
    {
        if (_view is null || States is null)
        {
            return;
        }

        var showsCards = CardsButton.IsChecked == true;
        States.SetShowsCards(_vault, _view.Id, showsCards);
        Log.Info("Action", showsCards ? "View cards shown" : "View cards hidden");
        Refresh();
    }

    /// <summary>The list's items wrap as cards, or stack as rows.</summary>
    private void UseCardsPanel(bool cards)
    {
        _rowsPanel ??= List.ItemsPanel;
        List.ItemsPanel = cards ? (ItemsPanelTemplate)FindResource("CardsPanel") : _rowsPanel;
        ScrollViewer.SetHorizontalScrollBarVisibility(List, cards ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto);
    }

    /// <summary>A card: its node's picture and name (as a row's, so it is renamed the same way) over its preview, read later.</summary>
    private ListBoxItem CardOf(Node node)
    {
        var visual = NodeVisual.Of(_graph!, node);
        var name = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        name.Children.Add(new NodeVisualView { Width = 20, Height = 20, Margin = new Thickness(0, 0, 8, 0), Visual = visual });
        name.Children.Add(new TextBlock { Text = node.Name, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        DockPanel.SetDock(name, Dock.Top);

        var body = new Decorator();
        var content = new DockPanel { LastChildFill = true };
        content.Children.Add(name);
        content.Children.Add(body);
        var card = new Border
        {
            Width = CardWidth,
            Height = CardHeight,
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)FindResource("CardStrokeColorDefaultBrush"),
            Background = NoteColors.Tint(visual.Color) ?? (Brush)FindResource("CardBackgroundFillColorDefaultBrush"),
            ClipToBounds = true,
            Child = content,
        };
        TextElement.SetFontSize(card, (double)FindResource("SecondaryFontSize"));

        var item = new ListBoxItem { Content = card, Tag = node, Style = (Style)FindResource("CardItem") };
        _unreadCards.Add((item, node, body));
        return item;
    }

    /// <summary>Reads the cards in view once the list is laid out (it has just been built).</summary>
    private void ReadCardsInViewSoon() => Dispatcher.BeginInvoke(ReadCardsInView, DispatcherPriority.Loaded);

    /// <summary>Fills each card now in view, not read yet, with its preview.</summary>
    private void ReadCardsInView()
    {
        if (_unreadCards.Count == 0 || _graph is not { } graph || FindScrollViewer(List) is not { } viewer)
        {
            return;
        }

        var viewport = new Rect(0, 0, viewer.ActualWidth, viewer.ActualHeight);
        foreach (var card in _unreadCards.Where(card => IsIn(card.Item, viewer, viewport)).ToList())
        {
            card.Body.Child = BodyOf(NodeCard.Of(graph, card.Node, ReadNote ?? (_ => null)));
            _unreadCards.Remove(card);
        }
    }

    private static bool IsIn(FrameworkElement item, Visual viewer, Rect viewport) =>
        item.IsVisible && item.IsDescendantOf(viewer) && viewport.IntersectsWith(item.TransformToAncestor(viewer).TransformBounds(new Rect(item.RenderSize)));

    private UIElement? BodyOf(NodeCard card) => card.Body switch
    {
        NoteCardBody note => NoteBodyOf(note.Preview),
        MissingCardBody missing => Caption(ContentStateOf(missing.Version).Summary),
        FileCardBody file when card.Visual.Thumbnail is not null => ThumbnailOf(card.Visual, file),
        FileCardBody file => Caption(FileText(file)),
        TextCardBody text => Caption(text.Text),
        _ => null,
    };

    private static StackPanel NoteBodyOf(NotePreview preview)
    {
        var lines = new StackPanel();
        foreach (var line in preview.Lines)
        {
            lines.Children.Add(new TextBlock { Text = line, TextTrimming = TextTrimming.CharacterEllipsis });
        }

        foreach (var item in preview.Items)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 12, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock { Text = item, TextTrimming = TextTrimming.CharacterEllipsis });
            lines.Children.Add(row);
        }

        if (preview.HasMore)
        {
            lines.Children.Add(Caption("…"));
        }

        if (preview.CheckedCount > 0)
        {
            lines.Children.Add(Caption(preview.CheckedCount == 1 ? "+ 1 checked" : $"+ {preview.CheckedCount} checked"));
        }

        return lines;
    }

    private static DockPanel ThumbnailOf(NodeVisual visual, FileCardBody file)
    {
        var caption = Caption(FileText(file));
        DockPanel.SetDock(caption, Dock.Bottom);
        var panel = new DockPanel();
        panel.Children.Add(caption);
        panel.Children.Add(new NodeVisualView { Width = double.NaN, Height = double.NaN, Visual = visual with { Color = NodeColor.None } });
        return panel;
    }

    private static string FileText(FileCardBody file) => $"{file.Kind}, {ByteSize.Format(file.Size, CultureInfo.CurrentCulture)}";

    private static TextBlock Caption(string text) => new() { Text = text, Opacity = 0.7, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
}
