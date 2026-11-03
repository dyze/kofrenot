using Avalonia.Controls;
using Avalonia.Interactivity;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;
using CoreTreeView = Kofrenot.Core.Views.TreeView;

namespace Kofrenot.Phone.Views;

/// <summary>
/// A node to put nodes under, picked on a screen of its own: first a tree view
/// (<see cref="ImportDestinations.TreeViews"/>), unless one is given, then down its tree, a node at a
/// time; Back goes up. The button takes the node reached, goes back to the screen before, then hands it on.
/// </summary>
public sealed partial class PickDestinationPage : UserControl, IPage
{
    private readonly Shell _shell;
    private readonly string _verb;
    private readonly Node? _fixedView;
    private readonly Func<ImportDestination, Task> _picked;
    private readonly List<TreeItem> _path = [];
    private Node? _view;

    /// <param name="verb">What the button says before the node's name ("Add into").</param>
    /// <param name="view">The tree view to browse; null to choose one first.</param>
    public PickDestinationPage(Shell shell, string title, string verb, Node? view, Func<ImportDestination, Task> picked)
    {
        _shell = shell;
        _verb = verb;
        _fixedView = view;
        _picked = picked;
        InitializeComponent();
        Bar.Title = title;
        if (view is not null)
        {
            Enter(view);
        }

        Refresh();
    }

    // Up the tree; out of it back to the views, unless the view was given.
    public bool GoBack()
    {
        if (_path.Count > 1)
        {
            _path.RemoveAt(_path.Count - 1);
        }
        else if (_view is not null && _fixedView is null)
        {
            _view = null;
            _path.Clear();
        }
        else
        {
            return false;
        }

        Refresh();
        return true;
    }

    public void SyncChanged()
    {
    }

    public void Refresh()
    {
        if (_shell.Services.Session.Current?.Graph is not { } graph)
        {
            return;
        }

        List<NodeRow> rows;
        if (_view is null)
        {
            rows = ImportDestinations.TreeViews(graph).Select(view => NodeRow.Of(graph, view)).ToList();
            ShowMessage(rows.Count == 0 ? "No tree view yet: add one on Windows (a view node), then choose here where to put nodes." : null);
        }
        else if (TreeOf(graph) is { } tree && _path.Count > 0)
        {
            rows = tree.ChildrenOf(_path[^1]).Select(child => NodeRow.Of(graph, child.Node, detail: child.CanExpand ? "›" : null)).ToList();
            ShowMessage(rows.Count == 0 ? "Nothing under this node." : null);
        }
        else
        {
            rows = [];
            ShowMessage("This tree is not there any more: removed, or its root not synced yet.");
        }

        List.ItemsSource = rows;
        PathText.Text = _view is null ? "Choose a tree view" : string.Join(" › ", [_view.Name, .. _path.Select(item => item.Node.Name)]);
        HereButton.IsVisible = _path.Count > 0;
        HereButton.Content = _path.Count > 0 ? $"{_verb} {_path[^1].Node.Name}" : null;
    }

    private CoreTreeView? TreeOf(KnowledgeGraph graph) =>
        _view is not null && ImportDestinations.TreeOf(graph, _view) is { } definition ? new CoreTreeView(graph, definition) : null;

    private void Enter(Node view)
    {
        _view = view;
        _path.Clear();
        if (_shell.Services.Session.Current?.Graph is { } graph && TreeOf(graph)?.Root is { } root)
        {
            _path.Add(root);
        }
    }

    private void ShowMessage(string? message)
    {
        Message.Text = message;
        Message.IsVisible = message is not null;
    }

    private void List_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (List.SelectedItem is not NodeRow row || _shell.Services.Session.Current?.Graph is not { } graph)
        {
            return;
        }

        List.SelectedItem = null;
        if (_view is null)
        {
            Enter(row.Node);
        }
        else if (TreeOf(graph)?.ChildrenOf(_path[^1]).FirstOrDefault(child => child.Node.Id == row.Node.Id) is { } child)
        {
            _path.Add(child);
        }

        Refresh();
    }

    private void Here_Click(object? sender, RoutedEventArgs e)
    {
        if (_view is null || _path.Count == 0)
        {
            return;
        }

        var destination = new ImportDestination(_view.Id, _path[^1].Node.Id);
        _shell.Back();
        _shell.Run(() => _picked(destination));
    }
}
