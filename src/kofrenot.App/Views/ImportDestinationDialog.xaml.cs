using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;
using Kofrenot.UI.Platform;
using CoreTreeView = Kofrenot.Core.Views.TreeView;

namespace Kofrenot.UI.Views;

/// <summary>Chooses where an import goes: a tree view (<see cref="ImportDestinations.TreeViews"/>), then a node of its tree.</summary>
public partial class ImportDestinationDialog : Window
{
    // The child of a node not expanded yet: it makes the node expandable.
    private const string NotRead = "…";

    private readonly KnowledgeGraph _graph;
    private CoreTreeView? _tree;

    private ImportDestinationDialog(Window owner, KnowledgeGraph graph, ImportDestination? current)
    {
        InitializeComponent();
        Owner = owner;
        _graph = graph;
        Tree.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler((_, e) => Fill((TreeViewItem)e.OriginalSource)));
        var views = ImportDestinations.TreeViews(graph);
        ViewsBox.ItemsSource = views;
        NoViewText.Visibility = views.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ViewsBox.SelectedItem = views.FirstOrDefault(view => view.Id == current?.View) ?? views.FirstOrDefault();
    }

    /// <summary>The destination chosen; null when cancelled.</summary>
    public static ImportDestination? Ask(Window owner, KnowledgeGraph graph, ImportDestination? current)
    {
        var dialog = new ImportDestinationDialog(owner, graph, current);
        return dialog.ShowDialog() == true ? dialog.Chosen : null;
    }

    private ImportDestination? Chosen =>
        ViewsBox.SelectedItem is Node view && (Tree.SelectedItem as TreeViewItem)?.Tag is TreeItem item ? new ImportDestination(view.Id, item.Node.Id) : null;

    private void ViewsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Tree.Items.Clear();
        _tree = ViewsBox.SelectedItem is Node view && ImportDestinations.TreeOf(_graph, view) is { } definition ? new CoreTreeView(_graph, definition) : null;
        if (_tree?.Root is { } root)
        {
            var item = ItemOf(root);
            Tree.Items.Add(item);
            item.IsSelected = true;
            item.IsExpanded = true;
        }

        ChooseButton.IsEnabled = Chosen is not null;
    }

    private void Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) => ChooseButton.IsEnabled = Chosen is not null;

    private TreeViewItem ItemOf(TreeItem item)
    {
        var view = new TreeViewItem { Header = item.Node.Name, Tag = item };
        if (item.CanExpand)
        {
            view.Items.Add(NotRead);
        }

        return view;
    }

    /// <summary>Reads the children of a node, once.</summary>
    private void Fill(TreeViewItem view)
    {
        if (view.Items is not [string] || view.Tag is not TreeItem item || _tree is null)
        {
            return;
        }

        view.Items.Clear();
        foreach (var child in _tree.ChildrenOf(item))
        {
            view.Items.Add(ItemOf(child));
        }
    }

    private void ChooseButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new WindowInteropHelper(this).Handle);
}
