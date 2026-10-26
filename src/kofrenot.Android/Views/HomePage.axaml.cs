using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Storage;
using Kofrenot.Core.Views;
using CoreTreeView = Kofrenot.Core.Views.TreeView;

namespace Kofrenot.Phone.Views;

/// <summary>A place in a tree view: its node's row, how deep, and whether its children show.</summary>
public sealed record TreeRow(TreeItem Item, NodeRow Row, int Depth, bool Expanded)
{
    private const double IndentStep = 20;
    private const double ToggleWidth = 40;

    public string Toggle => Expanded ? "▾" : "▸";

    public Thickness Indent => new(Depth * IndentStep, 0, 0, 0);

    /// <summary>A place without children lines up with those that have a toggle.</summary>
    public Thickness Shift => new(Item.CanExpand ? 0 : ToggleWidth, 0, 0, 0);
}

/// <summary>
/// The open vault: the search of the input box (pinned and recent nodes while it is empty), and
/// its views as tabs, opened on this phone. A tap on a node opens its details.
/// </summary>
public sealed partial class HomePage : UserControl, IPage
{
    private readonly Shell _shell;
    private readonly HashSet<string> _expanded = [];
    private Tab _tab;

    public HomePage(Shell shell, Tab? tab = null)
    {
        _shell = shell;
        _tab = tab ?? Tab.Search;
        InitializeComponent();
        Refresh();
    }

    private Vault? Vault => _shell.Services.Session.Current;

    public bool GoBack()
    {
        if (_tab.IsSearch && string.IsNullOrEmpty(SearchBox.Text))
        {
            return false;
        }

        // Back clears the search, then goes to the search tab.
        if (!string.IsNullOrEmpty(SearchBox.Text))
        {
            SearchBox.Text = string.Empty;
        }
        else
        {
            _tab = Tab.Search;
            Refresh();
        }

        return true;
    }

    public void Refresh()
    {
        if (Vault is not { } vault)
        {
            return;
        }

        VaultName.Text = vault.Graph.VaultName ?? "Vault";
        SyncChanged();
        ShowTabs(vault);
        ShowList(vault);
    }

    public void SyncChanged()
    {
        if (_shell.Services.Session.Selected?.Location.Store is { } store)
        {
            var status = _shell.Services.Sync.StatusOf(store);
            SyncDot.Fill = VaultsPage.DotOf(status.Level);
            SyncText.Text = status.Summary;
        }
    }

    private void ShowTabs(Vault vault)
    {
        var tabs = new TabList(_shell.Services.ViewTabs).Of(vault.Graph, vault.Id);
        if (!tabs.Contains(_tab))
        {
            _tab = Tab.Search;
        }

        Tabs.Children.Clear();
        foreach (var tab in tabs)
        {
            var button = new Button { Content = tab.Name, Tag = tab, Classes = { "tab", tab == _tab ? "accent" : "flat" } };
            button.Click += (_, _) => Select(tab);
            Tabs.Children.Add(button);
        }

        if (!_tab.IsSearch)
        {
            var close = new Button { Content = "✕", Classes = { "tab", "flat" } };
            close.Click += (_, _) => CloseTab(vault);
            Tabs.Children.Add(close);
        }

        if (new TabList(_shell.Services.ViewTabs).Closed(vault.Graph, vault.Id) is { Count: > 0 } closed)
        {
            var open = new Button { Content = "+", Classes = { "tab", "flat" } };
            var menu = new MenuFlyout();
            foreach (var view in closed)
            {
                var item = new MenuItem { Header = view.Name };
                item.Click += (_, _) => OpenTab(vault, view);
                menu.Items.Add(item);
            }

            open.Flyout = menu;
            Tabs.Children.Add(open);
        }
    }

    private void ShowList(Vault vault)
    {
        var graph = vault.Graph;
        var text = SearchBox.Text ?? string.Empty;
        Message.IsVisible = false;
        if (_tab.View is { } view)
        {
            List.ItemsSource = ViewRows(graph, view, text);
            return;
        }

        if (text.Length > 0)
        {
            var hits = GraphSearch.Find(graph, text, limit: 100);
            List.ItemsSource = hits.Select(hit => NodeRow.Of(graph, hit.Node, mark: hit.Mark)).ToList();
            ShowMessage(hits.Count == 0 ? "No node of that name." : null);
            return;
        }

        var pinned = graph.PinnedNodes;
        var recent = _shell.Services.Recent.Of(vault.Id)
            .Select(graph.FindNode)
            .OfType<Node>()
            .Where(node => !pinned.Contains(node))
            .ToList();
        var rows = new List<object>();
        if (pinned.Count > 0)
        {
            rows.Add(new HeaderRow("Pinned"));
            rows.AddRange(pinned.Select(node => NodeRow.Of(graph, node)));
        }

        if (recent.Count > 0)
        {
            rows.Add(new HeaderRow("Recent"));
            rows.AddRange(recent.Select(node => NodeRow.Of(graph, node)));
        }

        List.ItemsSource = rows;
        ShowMessage(rows.Count == 0 ? "Type a name to find a node. Pinned and recent nodes show here." : null);
    }

    // A view's nodes: a flat list, or a tree expanded by hand; searched, the places found.
    private List<object> ViewRows(KnowledgeGraph graph, Node view, string text)
    {
        switch (new ViewPlanner(graph).Load(view).Definition)
        {
            case FlatDefinition definition:
                var flat = new FlatView(graph, definition);
                var nodes = text.Length > 0 ? flat.Find(text) : flat.Nodes;
                ShowMessage(nodes.Count == 0 ? (text.Length > 0 ? "No node of that name." : $"No node of type {graph.Types.NodeTypeName(definition.NodeType)} yet.") : null);
                return [.. nodes.Select(node => NodeRow.Of(graph, node))];
            case TreeDefinition definition when new CoreTreeView(graph, definition) is { Root: { } root } tree:
                if (text.Length > 0)
                {
                    var found = tree.Find(text);
                    ShowMessage(found.Count == 0 ? "No node of that name." : null);
                    return [.. found.Select(item => NodeRow.Of(graph, item.Node, PathOf(graph, item)))];
                }

                var rows = new List<object>();
                AddTree(graph, tree, root, 0, rows);
                return rows;
            case TreeDefinition:
                ShowMessage("The node this tree starts from is not there: removed, or not synced yet.");
                return [];
            default:
                ShowMessage("This view has no definition yet: it is set in kofrenot on Windows.");
                return [];
        }
    }

    private void AddTree(KnowledgeGraph graph, CoreTreeView tree, TreeItem item, int depth, List<object> rows)
    {
        // The root shows its children until collapsed; the others once expanded.
        var expanded = _expanded.Contains(item.Key) ^ (depth == 0);
        rows.Add(new TreeRow(item, NodeRow.Of(graph, item.Node), depth, expanded));
        if (expanded && item.CanExpand)
        {
            foreach (var child in tree.ChildrenOf(item))
            {
                AddTree(graph, tree, child, depth + 1, rows);
            }
        }
    }

    // Where a place found is: the names above it.
    private static string PathOf(KnowledgeGraph graph, TreeItem item) =>
        string.Join(" › ", item.Path.SkipLast(1).Select(graph.FindNode).OfType<Node>().Select(node => node.Name));

    private void ShowMessage(string? message)
    {
        Message.Text = message;
        Message.IsVisible = message is not null;
    }

    private void Select(Tab tab)
    {
        _tab = tab;
        SearchBox.Text = string.Empty;
        Refresh();
    }

    private void OpenTab(Vault vault, Node view)
    {
        _shell.Services.ViewTabs.Open(vault.Id, view.Id);
        Select(new Tab(view));
    }

    private void CloseTab(Vault vault)
    {
        if (_tab.View is { } view)
        {
            _shell.Services.ViewTabs.Close(vault.Id, view.Id);
        }

        Select(Tab.Search);
    }

    private void Search_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (Vault is { } vault)
        {
            ShowList(vault);
        }
    }

    private void List_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var node = List.SelectedItem switch
        {
            NodeRow row => row.Node,
            TreeRow row => row.Item.Node,
            _ => null,
        };
        List.SelectedItem = null;
        if (node is not null)
        {
            _shell.Push(new NodePage(_shell, node));
        }
    }

    private void Toggle_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is TreeRow row && Vault is { } vault)
        {
            if (!_expanded.Remove(row.Item.Key))
            {
                _expanded.Add(row.Item.Key);
            }

            ShowList(vault);
        }
    }

    private void Vaults_Click(object? sender, RoutedEventArgs e) => _shell.Push(new VaultsPage(_shell));

    private void Sync_Click(object? sender, RoutedEventArgs e) => _shell.Run(_shell.Services.Sync.SyncNowAsync);

    private void Settings_Click(object? sender, RoutedEventArgs e) => _shell.Push(new SettingsPage(_shell));
}
