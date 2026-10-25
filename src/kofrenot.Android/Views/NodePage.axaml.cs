using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Notes;
using Kofrenot.Core.Storage;
using Kofrenot.Core.Views;

namespace Kofrenot.Phone.Views;

/// <summary>
/// A node's details, as in the main window: name, type, colour, links read from its side; a tap
/// on a link opens that node (back goes through the trail). A note's content is shown, fetched
/// when not here; a dataset is described; a view opens as a tab.
/// </summary>
public sealed partial class NodePage : UserControl, IPage
{
    private readonly Shell _shell;
    private readonly NodeBrowser _browser = new();

    public NodePage(Shell shell, Node node)
    {
        _shell = shell;
        InitializeComponent();
        if (Vault is { } vault)
        {
            _browser.Open(vault.Graph, node);
            Opened(vault, node);
        }

        Refresh();
    }

    private Vault? Vault => _shell.Services.Session.Current;

    public bool GoBack()
    {
        if (_browser.Depth <= 1)
        {
            return false;
        }

        _browser.Back();
        Refresh();
        return true;
    }

    public void Refresh()
    {
        // The graph read again: a sync may have changed the node.
        if (Vault is not { } vault || _browser.Details?.Node is not { } open || vault.Graph.FindNode(open.Id) is null)
        {
            return;
        }

        if (_browser.Graph != vault.Graph)
        {
            _browser.Open(vault.Graph, open);
        }

        var details = _browser.Details!;
        Trail.Text = string.Join(" › ", _browser.Trail);
        NodeName.Text = details.Name;
        NodeType.Text = details.Type;
        ColorDot.Fill = NodeLook.Brush(details.Color);
        ColorDot.IsVisible = details.Color != NodeColor.None;
        Pinned.IsVisible = details.IsPinned;
        ShowConflicts(details);
        ShowContent(vault, details.Node, details.Color);
        Links.ItemsSource = details.Links.Select(link => NodeRow.Of(vault.Graph, link.OtherNode, link.Type)).ToList();
        LinksHeader.IsVisible = details.Links.Count > 0;
    }

    private void ShowConflicts(NodeDetails details)
    {
        var conflicts = new List<string>();
        if (details.NameConflict.Count > 1)
        {
            conflicts.Add($"Named in {details.NameConflict.Count} ways: {string.Join(", ", details.NameConflict)}.");
        }

        if (details.TypeConflict.Count > 1)
        {
            conflicts.Add($"Of {details.TypeConflict.Count} types: {string.Join(", ", details.TypeConflict.Select(type => type.Name))}.");
        }

        Conflict.Text = string.Join("\n", conflicts.Append("Resolved in kofrenot on Windows."));
        Conflict.IsVisible = conflicts.Count > 0;
    }

    private void ShowContent(Vault vault, Node node, NodeColor color)
    {
        NoteBox.IsVisible = ContentText.IsVisible = OpenViewButton.IsVisible = false;
        var graph = vault.Graph;
        if (graph.NoteVersionsOf(node.Id) is { Count: > 0 } notes)
        {
            ShowNote(vault, node, notes, color);
        }
        else if (graph.DatasetVersionsOf(node.Id) is { Count: > 0 } datasets)
        {
            var files = datasets.Select(version => $"{version.File.Name}, {ByteSize.Format(version.File.Size)}");
            var conflict = datasets.Count > 1 ? $"{datasets.Count} versions, in conflict:\n" : string.Empty;
            ShowText($"{conflict}{string.Join("\n", files)}\n\nFiles are opened in kofrenot on Windows.");
        }
        else if (node.Type == TypeCatalog.View.Id)
        {
            OpenViewButton.IsVisible = true;
        }
    }

    // The latest version, then those in conflict with it; one not here says why.
    private void ShowNote(Vault vault, Node node, IReadOnlyList<NoteEdited> versions, NodeColor color)
    {
        var state = new NotePlanner(vault.Graph).Load(node, vault.ReadNote);
        if (state.IsMissing)
        {
            ShowText(vault.StateOf(versions[^1].Id).Describe("the note"));
            return;
        }

        Note.Children.Clear();
        if (state.Conflict.Count > 1)
        {
            foreach (var version in state.Conflict)
            {
                Note.Children.Add(new TextBlock { Classes = { "caption" }, Text = $"Version of {version.At.ToLocalTime():g}" });
                NoteView.Add(Note, version.Text ?? vault.StateOf(version.Id).Describe("this version"));
            }
        }
        else
        {
            NoteView.Add(Note, state.Text!);
        }

        NoteBox.Background = NodeLook.Brush(color) is ISolidColorBrush shade
            ? new SolidColorBrush(shade.Color, 0.25)
            : null;
        NoteBox.IsVisible = true;
    }

    private void ShowText(string text)
    {
        ContentText.Text = text;
        ContentText.IsVisible = true;
    }

    private void Opened(Vault vault, Node node) => _shell.Services.Recent.Opened(vault.Id, node.Id);

    private void Link_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is NodeRow row && Vault is { } vault)
        {
            if (!_browser.OpenLinked(row.Node))
            {
                _browser.Open(vault.Graph, row.Node);
            }

            Opened(vault, row.Node);
            Refresh();
        }
    }

    private void OpenView_Click(object? sender, RoutedEventArgs e)
    {
        if (Vault is { } vault && _browser.Details?.Node is { } view)
        {
            _shell.Services.ViewTabs.Open(vault.Id, view.Id);
            _shell.Show(new HomePage(_shell, new Tab(view)));
        }
    }

    private void Back_Click(object? sender, RoutedEventArgs e)
    {
        if (!GoBack())
        {
            _shell.Back();
        }
    }
}
