using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Kofrenot.Core;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;
using Kofrenot.Core.Views;
using CoreTreeView = Kofrenot.Core.Views.TreeView;

namespace Kofrenot.Phone.Views;

/// <summary>A place in a tree view: its node's row, how deep, and whether its children show.</summary>
public sealed record TreeRow(TreeItem Item, NodeRow Row, int Depth, bool Expanded)
{
    private const double IndentStep = 20;
    private const double ToggleWidth = 48;

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

    // What the search box's text adds; null when it adds nothing.
    private CreationForm? _adding;

    public HomePage(Shell shell, Tab? tab = null)
    {
        _shell = shell;
        _tab = tab ?? Tab.Search;
        InitializeComponent();
        WatchSearchBox();
        Refresh();
    }

    private Vault? Vault => _shell.Services.Session.Current;

    public bool GoBack()
    {
        if (_tab.IsSearch && _chips.IsEmpty)
        {
            return false;
        }

        // Back clears the search, then goes to the search tab.
        if (!_chips.IsEmpty)
        {
            ClearSearch();
        }
        else
        {
            _tab = Tab.Search;
        }

        Refresh();

        return true;
    }

    public void Refresh()
    {
        if (Vault is not { } vault)
        {
            return;
        }

        // Notes and files are edited here; a reader, or a kofrenot older than the vault, edits nothing.
        ReadOnlyBanner.IsVisible = _shell.Services.Session.IsReadOnly;
        AddFilesButton.IsVisible = !_shell.Services.Session.IsReadOnly;
        ReadOnlyText.Text = _shell.Services.Session.NeedsNewerKofrenotToWrite
            ? $"{VaultFormat.ReadOnlyMessage} {ProjectLinks.Releases}"
            : "Shared with you to read only.";
        SyncChanged();
        ShowTabs(vault);
        ShowList(vault);
    }

    public void SyncChanged()
    {
        if (_shell.Services.Session.Selected?.Location.Store is { } store)
        {
            SyncDot.Fill = VaultsPage.DotOf(_shell.Services.Sync.StatusOf(store).Level);
        }
    }

    private void ShowList(Vault vault)
    {
        var graph = vault.Graph;
        var text = _chips.Text;
        Message.IsVisible = false;
        ShowAdd(graph, text);
        ShowChips(graph);
        if (_tab.View is { } view)
        {
            var viewRows = ViewRows(graph, view, text);
            List.ItemsSource = viewRows;
            ShowViewBar(viewRows.Count);
            return;
        }

        if (text.Length > 0 && !QueryParser.IsQuery(text) && LinkSearch.Find(graph, text) is { } links)
        {
            List.ItemsSource = links.Select(hit => NodeRow.Of(graph, hit.Found, detail: $"{hit.From.Name} {hit.Verb?.Name} {hit.To.Name} · {hit.Field.Label}: {hit.Field.Display}")).ToList();
            ShowMessage(links.Count == 0 ? "No link has this qualifier." : null);
            return;
        }

        // A query (person:, contained by Kitchen, changed: this week), as the input box on Windows.
        if (QueryParser.Parse(text, graph) is { IsEmpty: true })
        {
            List.ItemsSource = QueryCompletion.Starts(graph).Select(start => new QueryStartRow(start.Insert.Trim(), start.Detail, start.Insert)).ToList();
            ShowMessage(QueryCompletion.Hint);
            return;
        }

        if (QueryParser.Parse(text, graph) is { } query)
        {
            var found = NodeQuerySearch.Find(graph, query, _shell.Services.Recent.Of(vault.Id), DateTimeOffset.UtcNow, TimeZoneInfo.Local);
            List.ItemsSource = found.Hits.Select(hit => NodeRow.Of(graph, hit.Node, detail: hit.Detail)).ToList();
            ShowMessage(found.Problem ?? (found.Total == 0 ? "No node found." : null));
            return;
        }

        if (text.Length > 0)
        {
            var hits = GraphSearch.Find(graph, text, limit: 100);
            List.ItemsSource = hits.Select(hit => NodeRow.Of(graph, hit.Node, mark: hit.Mark, detail: hit.Field)).ToList();
            ShowMessage(hits.Count == 0 ? "No node of that name." : null);
            return;
        }

        // Pinned, then recent (opened on this phone or changed on any device), as the input box on Windows.
        var start = StartNodes.Of(graph, _shell.Services.Recent.Of(vault.Id), DateTimeOffset.UtcNow);
        var rows = new List<object>();
        AddSection(rows, "Pinned", start.Pinned.Select(node => NodeRow.Of(graph, node)));
        AddSection(rows, "Recent", start.Recent.Select(recent => NodeRow.Of(graph, recent.Node, detail: recent.Detail)));
        List.ItemsSource = rows;
        ShowMessage(rows.Count == 0 ? "Type a name to find a node, or what to add: Dune relates to Frank Herbert. Pinned and recent nodes show here." : null);
    }

    private static void AddSection(List<object> rows, string header, IEnumerable<NodeRow> section)
    {
        var nodes = section.ToList();
        if (nodes.Count > 0)
        {
            rows.Add(new HeaderRow(header));
            rows.AddRange(nodes);
        }
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
            case GraphDefinition:
                ShowMessage("A graph is shown in kofrenot on Windows.");
                return [];
            case QueryDefinition definition:
                var query = new QueryView(graph, definition);
                var queryRows = text.Length > 0 ? query.Find(text) : query.Rows;
                ShowMessage(query.IsBroken ? "The node this query names is not there: removed, or not synced yet."
                    : queryRows.Count == 0 ? (text.Length > 0 ? "No node of that name." : $"No node found by “{QueryDraft.TextOf(definition, graph)}”.")
                    : null);
                return [.. queryRows.Select(row => NodeRow.Of(graph, row.Node, query.LineOf(row)))];
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

    // The search tab's text read as the input box reads it: what it adds, offered under the box.
    private void ShowAdd(KnowledgeGraph graph, string text)
    {
        _adding = _tab.IsSearch && !_shell.Services.Session.IsReadOnly && QueryParser.Parse(text, graph) is null
            && InputParser.Parse(text, graph) is { } request
            ? new CreationForm(graph, request)
            : null;
        AddButton.IsVisible = _adding is { CanAdd: true };
        AddText.Text = _adding is null ? string.Empty : $"＋ {_adding.Status}";
    }

    private void Add_Click(object? sender, RoutedEventArgs e) => ShowCreation();

    /// <summary>A tap on what the text adds, or Enter: all of it as labels, then its creation screen. False when it adds nothing.</summary>
    private bool ShowCreation()
    {
        if (_adding is not { CanAdd: true } form || Vault is not { } vault)
        {
            return false;
        }

        LabelAll(vault.Graph);
        _shell.Services.Log.Info("Action", form.IsLink ? "Creation screen shown for a link" : "Creation screen shown for a node");
        _shell.Push(new CreationPage(_shell, form, added: ClearSearch));
        return true;
    }

    private void List_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selected = List.SelectedItem;
        var node = selected switch
        {
            NodeRow row => row.Node,
            TreeRow row => row.Item.Node,
            _ => null,
        };
        List.SelectedItem = null;
        if (selected is QueryStartRow start && Vault is { } vault)
        {
            _shell.Services.Log.Info("Action", "Start a query from the list");
            _chips.Replace(start.Insert);
            SetSearchText(_chips.Tail);
            ShowList(vault);
            SearchBox.Focus();
            return;
        }

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

    // ☰, as a drawer: the open vault and where its sync stands, the other vaults to open, the list of vaults, and Settings.
    private void Vault_Click(object? sender, RoutedEventArgs e)
    {
        var session = _shell.Services.Session;
        if (session.Selected is not { Location.Store: { } store } selected)
        {
            return;
        }

        var status = _shell.Services.Sync.StatusOf(store);
        var others = session.Vaults
            .Where(vault => vault.Location.Store is { } other && vault != selected)
            .Select(vault => new SheetChoice(session.NameOf(vault), () => _shell.Open(vault.Location), VaultsPage.DotOf(_shell.Services.Sync.StatusOf(vault.Location.Store!).Level)));
        _shell.ShowSheet($"{session.NameOf(selected)} · {status.Summary}",
        [
            new("Sync now", () => _shell.Run(_shell.Services.Sync.SyncNowAsync), VaultsPage.DotOf(status.Level)),
            .. others,
            new("Vaults…", () => _shell.Push(new VaultsPage(_shell)), Icon: "vault"),
            new("Settings", () => _shell.Push(new SettingsPage(_shell)), Icon: "settings"),
        ]);
    }
}
