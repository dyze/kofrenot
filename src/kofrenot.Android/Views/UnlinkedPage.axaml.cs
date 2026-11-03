using Avalonia.Controls;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>
/// The unlinked nodes (<see cref="UnlinkedNodes"/>) of a tree view's vault, filtered by name: a tap
/// opens one, or moves it into the tree (<see cref="PickDestinationPage"/>, from the tree's root),
/// linked through the tree's link type. The filter is never logged.
/// </summary>
public sealed partial class UnlinkedPage : UserControl, IPage
{
    private readonly Shell _shell;
    private readonly Node _view;

    public UnlinkedPage(Shell shell, Node view)
    {
        _shell = shell;
        _view = view;
        InitializeComponent();
        Bar.Title = "Unlinked nodes";
        Refresh();
    }

    public bool GoBack() => false;

    public void SyncChanged() => Refresh();

    public void Refresh()
    {
        if (_shell.Services.Session.Current?.Graph is not { } graph)
        {
            return;
        }

        var text = SearchBox.Text ?? string.Empty;
        var list = new UnlinkedNodes(graph).Find(text);
        var now = DateTimeOffset.UtcNow;
        List.ItemsSource = list.Shown.Select(node => NodeRow.Of(graph, node, detail: $"Added {TimeAgo.Of(node.Created, now)}")).ToList();
        Message.Text = (list.Count, list.IsCut, text.Trim().Length > 0) switch
        {
            (0, _, true) => "No unlinked node of this name.",
            (0, _, false) => "Every node has a parent.",
            (_, true, _) => $"The newest {list.Shown.Count} of {list.Count}: type to find others. A tap moves one into {_view.Name}.",
            _ => $"{list.Count} without a parent. A tap moves one into {_view.Name}.",
        };
    }

    private void Search_TextChanged(object? sender, TextChangedEventArgs e) => Refresh();

    private void List_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (List.SelectedItem is not NodeRow row)
        {
            return;
        }

        List.SelectedItem = null;
        var readOnly = _shell.Services.Session.IsReadOnly;
        _shell.ShowSheet(row.Node.Name,
        [
            new($"Move into {_view.Name}…", () => _shell.Push(new PickDestinationPage(_shell, $"Move {row.Node.Name}", "Move into", _view, destination => MoveAsync(row.Node, destination))),
                IsEnabled: !readOnly, Icon: "view"),
            new("Open", () => _shell.Push(new NodePage(_shell, row.Node)), Icon: "open"),
        ]);
    }

    private Task MoveAsync(Node node, ImportDestination destination)
    {
        if (_shell.Services.Session.Current?.Graph is not { } graph || ImportDestinations.Resolve(graph, destination) is not { } resolved)
        {
            return Task.CompletedTask;
        }

        var changes = new UnlinkedNodes(graph).PutUnder([node], resolved.Under);
        if (changes.Count == 0)
        {
            _shell.Tell($"{node.Name} is above {resolved.Node.Name}: it cannot go under it.");
            return Task.CompletedTask;
        }

        var answers = new PreparedAnswers();
        _shell.Services.Session.Write(answers, changes, "Unlinked nodes put in a tree (1)");
        _shell.Tell(answers.Told);
        Refresh();
        return Task.CompletedTask;
    }
}
