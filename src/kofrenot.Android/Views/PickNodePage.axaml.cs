using Avalonia.Controls;
using Kofrenot.Core.Graph;

namespace Kofrenot.Phone.Views;

/// <summary>
/// A node picked on a screen of its own, found by typing its name: the rows offered for the text (a
/// node shown first, preselected), else why there are none. A tap goes back to the screen before, then
/// hands the node on.
/// </summary>
public sealed partial class PickNodePage : UserControl, IPage
{
    private readonly Shell _shell;
    private readonly Func<KnowledgeGraph, string, IReadOnlyList<NodeRow>> _rowsOf;
    private readonly string _empty;
    private readonly Func<Node, Task> _picked;

    public PickNodePage(Shell shell, string title, Func<KnowledgeGraph, string, IReadOnlyList<NodeRow>> rowsOf, string empty, Func<Node, Task> picked)
    {
        _shell = shell;
        _rowsOf = rowsOf;
        _empty = empty;
        _picked = picked;
        InitializeComponent();
        Bar.Title = title;
        Refresh();
    }

    public bool GoBack() => false;

    public void SyncChanged()
    {
    }

    public void Refresh()
    {
        if (_shell.Services.Session.Current?.Graph is not { } graph)
        {
            return;
        }

        var rows = _rowsOf(graph, SearchBox.Text ?? string.Empty);
        List.ItemsSource = rows;
        Message.Text = _empty;
        Message.IsVisible = rows.Count == 0;
    }

    private void Search_TextChanged(object? sender, TextChangedEventArgs e) => Refresh();

    private void List_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (List.SelectedItem is not NodeRow row)
        {
            return;
        }

        List.SelectedItem = null;
        _shell.Back();
        _shell.Run(() => _picked(row.Node));
    }
}
