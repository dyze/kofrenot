using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;
using CoreTreeView = Kofrenot.Core.Views.TreeView;

namespace Kofrenot.UI.Views;

/// <summary>
/// A view tab's content, read from the graph at each <see cref="Show"/>: a tree, whose children
/// are read when a node is expanded, or a list. The nodes expanded are kept on this device
/// (<see cref="ViewStates"/>); a refresh also keeps the one selected, by its path.
/// </summary>
public partial class ViewPanel : UserControl
{
    // The child of a node not expanded yet: it makes the node expandable.
    private const string NotRead = "…";

    private const double CutOpacity = 0.45;

    private KnowledgeGraph? _graph;
    private Guid _vault;
    private CoreTreeView? _tree;
    private TreeDefinition? _definition;
    private FlatView? _list;
    private Node? _view;
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

    /// <summary>The nodes expanded in each view, kept on this device.</summary>
    public ViewStates? States { get; set; }

    public void Show(Guid vault, KnowledgeGraph graph, Node view)
    {
        // The rows are built again: a name edited in place is given up.
        StopRename();
        if (_view?.Id != view.Id || _vault != vault)
        {
            _selectedPath = null;
            _selectedNode = null;
            ClearSearch();
        }

        _vault = vault;
        _view = view;
        _graph = graph;
        _definition = null;
        _listType = null;
        _list = null;
        switch (new ViewPlanner(graph).Load(view).Definition)
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
                _listType = graph.Types.FindNodeType(definition.NodeType);
                _list = new FlatView(graph, definition);
                var rows = _list.Nodes.Select(RowOf).ToList();
                List.Items.Clear();
                rows.ForEach(row => List.Items.Add(row));
                List.SelectedItem = rows.FirstOrDefault(row => NodeOf(row)?.Id == _selectedNode);
                ShowOnly(List);
                if (rows.Count == 0)
                {
                    ShowMessage($"No node of type {graph.Types.NodeTypeName(definition.NodeType)} yet.");
                }

                break;
            default:
                ShowMessage("This view has no definition yet: ⚙ on its tab says what it shows.");
                break;
        }

        ShowActions();
        RefreshFound();
    }

    /// <summary>Gives the keyboard to the tree or the list, on its selected node (the first one by default).</summary>
    /// <remarks>
    /// By what is shown (<see cref="UIElement.Visibility"/>), not <see cref="UIElement.IsVisible"/>, which
    /// waits for the layout: the view may just have been shown again (Esc from the details). Laid out
    /// first, so the row exists and takes the keyboard, not the tree itself.
    /// </remarks>
    public void FocusContent()
    {
        UpdateLayout();
        if (Tree.Visibility == Visibility.Visible)
        {
            var item = Tree.SelectedItem as TreeViewItem ?? Tree.Items.OfType<TreeViewItem>().FirstOrDefault();
            if (item is not null)
            {
                item.IsSelected = true;
                item.Focus();
            }
        }
        else if (List.Visibility == Visibility.Visible)
        {
            List.SelectedIndex = Math.Max(List.SelectedIndex, 0);
            List.UpdateLayout();
            (List.SelectedItem as ListBoxItem)?.Focus();
        }
    }

    private TreeViewItem ItemOf(TreeItem item)
    {
        var path = item.Key;
        // No focus frame: it would surround the item with everything expanded under it; the selection shows the keyboard.
        var view = new TreeViewItem { Header = HeaderOf(item.Node, isCut: _taken is { IsCut: true } taken && taken.Item.Key == path), Tag = item, FocusVisualStyle = null };
        if (item.CanExpand)
        {
            view.Items.Add(NotRead);
        }

        if (path == _selectedPath)
        {
            view.IsSelected = true;
        }

        if (item.CanExpand && IsExpanded(path))
        {
            Fill(view);
            view.IsExpanded = true;
        }

        return view;
    }

    /// <param name="isCut">The node cut (Ctrl+X), not pasted yet: shown dimmed.</param>
    private StackPanel HeaderOf(Node node, bool isCut)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal, Opacity = isCut ? CutOpacity : 1 };
        header.Children.Add(new NodeVisualView { Width = 24, Height = 24, Margin = new Thickness(0, 1, 10, 1), Visual = NodeVisual.Of(_graph!, node) });
        header.Children.Add(new TextBlock { Text = node.Name, VerticalAlignment = VerticalAlignment.Center });
        return header;
    }

    /// <summary>A row of a list: the node pictured, as in a tree (no focus frame either).</summary>
    private ListBoxItem RowOf(Node node) => new() { Content = HeaderOf(node, isCut: false), Tag = node, FocusVisualStyle = null };

    private static Node? NodeOf(object? row) => (row as ListBoxItem)?.Tag as Node;

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
            SetExpanded(item.Key, isExpanded: true);
        }
    }

    private void Tree_Collapsed(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { Tag: TreeItem item })
        {
            SetExpanded(item.Key, isExpanded: false);
        }
    }

    private void Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (Tree.SelectedItem is TreeViewItem { Tag: TreeItem item })
        {
            _selectedPath = item.Key;
        }

        UpdateActions();
    }

    private void Content_KeyDown(object sender, KeyEventArgs e)
    {
        // While renaming, the keys are the box's.
        if (IsRenaming)
        {
            return;
        }

        if (e.Key == Key.F2 && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = StartRename();
        }
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = OpenSelected();
        }
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            FocusSearch();
            e.Handled = true;
        }
        else if (sender == Tree)
        {
            e.Handled = HandleTreeKey(e.Key);
        }
    }

    // In a name being edited, a double-click selects a word.
    private void Content_MouseDoubleClick(object sender, MouseButtonEventArgs e) => e.Handled = !IsRenaming && OpenSelected();

    /// <summary>The node selected in the tree or the list, or null.</summary>
    private Node? SelectedNode => Tree.Visibility == Visibility.Visible
        ? (Tree.SelectedItem as TreeViewItem)?.Tag is TreeItem item ? item.Node : null
        : NodeOf(List.SelectedItem);

    private bool OpenSelected()
    {
        if (SelectedNode is not { } node)
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

    private bool IsExpanded(string key) => _view is not null && States?.ExpandedIn(_vault, _view.Id).Contains(key) == true;

    private void SetExpanded(string key, bool isExpanded)
    {
        if (_view is not null)
        {
            States?.SetExpanded(_vault, _view.Id, key, isExpanded);
        }
    }
}
