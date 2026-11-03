using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>
/// The unlinked nodes (<see cref="UnlinkedNodes"/>) beside a tree: filtered by name, the newest
/// first, at most <see cref="UnlinkedNodes.MaxShown"/>. The rows selected are dragged as their ids
/// (<see cref="DragFormat"/>), for the tree to put them under a node. The filter is never logged.
/// </summary>
public partial class UnlinkedPane : UserControl
{
    /// <summary>The ids of the nodes dragged, joined by ','.</summary>
    public const string DragFormat = "kofrenot.Unlinked";

    private KnowledgeGraph? _graph;
    private Point? _dragStart;

    // A plain click on one of several selected: it selects that one alone when released, unless dragged.
    private ListBoxItem? _pressedInSelection;

    public UnlinkedPane() => InitializeComponent();

    /// <summary>Asks to open a node's details.</summary>
    public event Action<Node>? OpenNode;

    /// <summary>Reads the unlinked nodes again, keeping the filter and the rows selected.</summary>
    public void Show(KnowledgeGraph graph)
    {
        _graph = graph;
        var selected = List.SelectedItems.Cast<ListBoxItem>().Select(row => ((Node)row.Tag).Id).ToHashSet();
        var list = new UnlinkedNodes(graph).Find(FilterBox.Text);
        List.Items.Clear();
        foreach (var node in list.Shown)
        {
            var row = RowOf(node);
            List.Items.Add(row);
            row.IsSelected = selected.Contains(node.Id);
        }

        var filtered = FilterBox.Text.Trim().Length > 0;
        CountText.Text = list.IsCut ? $"{list.Shown.Count} of {list.Count}" : list.Count.ToString(System.Globalization.CultureInfo.CurrentCulture);
        HintText.Text = (list.Count, list.IsCut, filtered) switch
        {
            (0, _, true) => "No unlinked node of this name.",
            (0, _, false) => "Every node has a parent.",
            (_, true, _) => "The newest are shown: type to find others. Drag them onto a node of the tree.",
            _ => "Drag them onto a node of the tree to put them there.",
        };
    }

    /// <summary>The nodes of a drag from this pane, or null for another drag.</summary>
    public static IReadOnlyList<Node>? Dragged(DragEventArgs e, KnowledgeGraph graph) =>
        e.Data.GetData(DragFormat) is string ids
            ? ids.Split(',').Select(id => Guid.TryParse(id, out var parsed) ? graph.FindNode(parsed) : null).OfType<Node>().ToList()
            : null;

    private ListBoxItem RowOf(Node node)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new NodeVisualView { Width = 20, Height = 20, Margin = new Thickness(0, 1, 8, 1), Visual = NodeVisual.Of(_graph!, node) });
        content.Children.Add(new TextBlock { Text = node.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        return new ListBoxItem { Content = content, Tag = node, FocusVisualStyle = null, ToolTip = $"Added {TimeAgo.Of(node.Created, DateTimeOffset.UtcNow)}" };
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterPlaceholder.Visibility = FilterBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_graph is not null)
        {
            Show(_graph);
        }
    }

    private void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RowAt(e.OriginalSource)?.Tag is Node node)
        {
            OpenNode?.Invoke(node);
        }
    }

    private void List_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = RowAt(e.OriginalSource);
        _dragStart = row is null ? null : e.GetPosition(List);

        // Several selected: a plain press keeps them, to drag them all.
        _pressedInSelection = row is { IsSelected: true } && List.SelectedItems.Count > 1 && Keyboard.Modifiers == ModifierKeys.None ? row : null;
        e.Handled = _pressedInSelection is not null;
    }

    private void List_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragStart is not { } start || List.SelectedItems.Count == 0)
        {
            return;
        }

        var moved = e.GetPosition(List) - start;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _dragStart = null;
        _pressedInSelection = null;
        var ids = string.Join(',', List.SelectedItems.Cast<ListBoxItem>().Select(row => ((Node)row.Tag).Id));
        DragDrop.DoDragDrop(List, new DataObject(DragFormat, ids), DragDropEffects.Link);
    }

    private void List_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragStart = null;
        if (_pressedInSelection is { } pressed)
        {
            _pressedInSelection = null;
            List.SelectedItems.Clear();
            pressed.IsSelected = true;
        }
    }

    /// <summary>The row an event comes from, or null off the rows.</summary>
    private static ListBoxItem? RowAt(object source)
    {
        var element = source as DependencyObject;
        while (element is not null and not ListBoxItem)
        {
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }

        return element as ListBoxItem;
    }
}
