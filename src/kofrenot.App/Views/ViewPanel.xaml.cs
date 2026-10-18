using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;
using CoreTreeView = Kofrenot.Core.Views.TreeView;

namespace Kofrenot.UI.Views;

/// <summary>A node in a view's list, with its picture.</summary>
public sealed record ViewRow(Node Node, NodeVisual Visual);

/// <summary>
/// A view tab's content, read from the graph at each <see cref="Show"/>: a tree, whose children
/// are read when a node is expanded, or a list. A refresh keeps the nodes expanded and the one
/// selected, by their path.
/// </summary>
public partial class ViewPanel : UserControl
{
    // The child of a node not expanded yet: it makes the node expandable.
    private const string NotRead = "…";

    private readonly HashSet<string> _expanded = [];
    private KnowledgeGraph? _graph;
    private CoreTreeView? _tree;
    private TreeDefinition? _definition;
    private Guid? _view;
    private string? _selectedPath;
    private Guid? _selectedNode;

    public ViewPanel()
    {
        InitializeComponent();
        Tree.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(Tree_Expanded));
        Tree.AddHandler(TreeViewItem.CollapsedEvent, new RoutedEventHandler(Tree_Collapsed));
    }

    /// <summary>Asks to open a node's details.</summary>
    public event Action<Node>? OpenNode;

    public void Show(KnowledgeGraph graph, Node view)
    {
        if (_view != view.Id)
        {
            _view = view.Id;
            _expanded.Clear();
            _selectedPath = null;
            _selectedNode = null;
        }

        _graph = graph;
        _definition = null;
        switch (TabList.DefinitionOf(graph, view))
        {
            case TreeDefinition definition when new CoreTreeView(graph, definition) is { Root: { } root } tree:
                _tree = tree;
                _definition = definition;
                Tree.Items.Clear();
                Tree.Items.Add(ItemOf(root));
                ShowOnly(Tree);
                break;
            case TreeDefinition:
                ShowMessage("The node this tree starts from is not there: removed, or not synced yet.");
                break;
            case FlatDefinition definition:
                var rows = new FlatView(graph, definition).Nodes.Select(node => new ViewRow(node, NodeVisual.Of(graph, node))).ToList();
                List.ItemsSource = rows;
                List.SelectedItem = rows.FirstOrDefault(row => row.Node.Id == _selectedNode);
                ShowOnly(List);
                if (rows.Count == 0)
                {
                    ShowMessage($"No node of type {graph.Types.NodeTypeName(definition.NodeType)} yet.");
                }

                break;
            default:
                ShowMessage("This view has no definition yet.");
                break;
        }
    }

    /// <summary>Gives the keyboard to the tree or the list, on its selected node (the first one by default).</summary>
    public void FocusContent()
    {
        if (Tree.IsVisible)
        {
            var item = Tree.SelectedItem as TreeViewItem ?? Tree.Items.OfType<TreeViewItem>().FirstOrDefault();
            if (item is not null)
            {
                item.IsSelected = true;
                item.Focus();
            }
        }
        else if (List.IsVisible)
        {
            List.SelectedIndex = Math.Max(List.SelectedIndex, 0);
            List.UpdateLayout();
            (List.ItemContainerGenerator.ContainerFromIndex(List.SelectedIndex) as ListBoxItem)?.Focus();
        }
    }

    private TreeViewItem ItemOf(TreeItem item)
    {
        var path = PathOf(item);
        var view = new TreeViewItem { Header = HeaderOf(item.Node), Tag = item };
        if (item.CanExpand)
        {
            view.Items.Add(NotRead);
        }

        if (path == _selectedPath)
        {
            view.IsSelected = true;
        }

        if (item.CanExpand && _expanded.Contains(path))
        {
            Fill(view);
            view.IsExpanded = true;
        }

        return view;
    }

    private StackPanel HeaderOf(Node node)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new NodeVisualView { Width = 24, Height = 24, Margin = new Thickness(0, 1, 10, 1), Visual = NodeVisual.Of(_graph!, node) });
        header.Children.Add(new TextBlock { Text = node.Name, VerticalAlignment = VerticalAlignment.Center });
        return header;
    }

    /// <summary>Reads the children of a node, once.</summary>
    private void Fill(TreeViewItem view)
    {
        if (view.Items is not [string] || view.Tag is not TreeItem item)
        {
            return;
        }

        view.Items.Clear();
        foreach (var child in _tree!.ChildrenOf(item))
        {
            view.Items.Add(ItemOf(child));
        }
    }

    private void Tree_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { Tag: TreeItem item } view)
        {
            Fill(view);
            _expanded.Add(PathOf(item));
        }
    }

    private void Tree_Collapsed(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { Tag: TreeItem item })
        {
            _expanded.Remove(PathOf(item));
        }
    }

    private void Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (Tree.SelectedItem is TreeViewItem { Tag: TreeItem item })
        {
            _selectedPath = PathOf(item);
        }
    }

    private void Content_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = OpenSelected();
        }
        else if (sender == Tree)
        {
            e.Handled = HandleTreeKey(e.Key);
        }
    }

    private void Content_MouseDoubleClick(object sender, MouseButtonEventArgs e) => e.Handled = OpenSelected();

    private bool OpenSelected()
    {
        var node = Tree.IsVisible
            ? (Tree.SelectedItem as TreeViewItem)?.Tag is TreeItem item ? item.Node : null
            : (List.SelectedItem as ViewRow)?.Node;
        if (node is null)
        {
            return false;
        }

        _selectedNode = node.Id;
        OpenNode?.Invoke(node);
        return true;
    }

    private void ShowOnly(UIElement content)
    {
        MessageText.Visibility = Visibility.Collapsed;
        Tree.Visibility = content == Tree ? Visibility.Visible : Visibility.Collapsed;
        List.Visibility = content == List ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowMessage(string message)
    {
        ShowOnly(MessageText);
        MessageText.Text = message;
        MessageText.Visibility = Visibility.Visible;
    }

    private static string PathOf(TreeItem item) => string.Join('/', item.Path);
}
