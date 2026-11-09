using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>
/// A query view (<see cref="QueryView"/>): the nodes its query finds, as a table, the list's View a <see cref="GridView"/>:
/// Name (the node pictured), then each column (<i>written by</i> → <i>Frank Herbert</i>, a field, a qualifier). Header and
/// cells are the same columns, so they line up; each can be resized. A click on a header sorts by it, again the other way
/// (<see cref="QuerySort"/>, kept per view in <see cref="ViewStates"/>); its header shows ▲ or ▼.
/// </summary>
public partial class ViewPanel
{
    private QueryView? _query;

    // The widths of the table's columns, kept while the view is shown again (sorted, synced): NaN for one fitted to its content.
    private IReadOnlyList<double> _queryWidths = [];

    private QuerySort QuerySort => _view is not null && States is not null ? States.SortOf(_vault, _view.Id) : QuerySort.ByName;

    private void ShowQuery(KnowledgeGraph graph, QueryDefinition definition)
    {
        _query = new QueryView(graph, definition);
        if (_query.IsBroken)
        {
            ShowMessage("The node this query names is not there: removed, or not synced yet.");
            return;
        }

        UseCardsPanel(false);
        var sort = QuerySort;
        List.View = TableOf(_query.Headers, sort);
        KeepWidths((GridView)List.View);
        var rows = _query.RowsBy(sort).Select(QueryRowOf).ToList();
        List.Items.Clear();
        rows.ForEach(row => List.Items.Add(row));
        List.SelectedItem = rows.FirstOrDefault(row => NodeOf(row)?.Id == _selectedNode);
        ShowOnly(List);
        if (rows.Count == 0)
        {
            ShowMessage($"No node found by “{QueryDraft.TextOf(definition, graph)}”.");
        }
    }

    // Name, then a column per header, each as wide as its content to start with; the one sorted by marked ▲ or ▼.
    private static GridView TableOf(IReadOnlyList<string> headers, QuerySort sort)
    {
        string HeaderOf(int column, string text) => column == sort.Column ? $"{text} {(sort.Descending ? "▼" : "▲")}" : text;

        var name = new FrameworkElementFactory(typeof(ContentPresenter));
        name.SetBinding(ContentPresenter.ContentProperty, new Binding(nameof(QueryCells.Name)));
        var table = new GridView { AllowsColumnReorder = false };
        table.Columns.Add(new GridViewColumn { Header = HeaderOf(QuerySort.NameColumn, "Name"), CellTemplate = new DataTemplate { VisualTree = name } });
        for (var index = 0; index < headers.Count; index++)
        {
            table.Columns.Add(new GridViewColumn { Header = HeaderOf(index + 1, headers[index]), DisplayMemberBinding = new Binding($"{nameof(QueryCells.Cells)}[{index}]") });
        }

        return table;
    }

    // The widths dragged before, when the columns are still as many.
    private void KeepWidths(GridView table)
    {
        if (_queryWidths.Count != table.Columns.Count)
        {
            return;
        }

        foreach (var (column, width) in table.Columns.Zip(_queryWidths))
        {
            column.Width = width;
        }
    }

    // A click on a column's header (not on the space after the last): sorted by it, or the other way.
    private void QueryHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader { Column: { } column } || List.View is not GridView table || _view is null || States is null)
        {
            return;
        }

        States.SetSort(_vault, _view.Id, QuerySort.ClickedOn(table.Columns.IndexOf(column)));
        Log.Info("Action", "Query view sorted");
        Refresh();
    }

    // A row: the node pictured with its name (so renaming finds it), then its cells; tagged with its node, as a list's row.
    private ListViewItem QueryRowOf(QueryRow row) =>
        new() { Content = new QueryCells(NamePanelOf(row.Node), row.Cells), Tag = row.Node, Style = (Style)FindResource("QueryRowItem") };

    /// <summary>What a query view's row shows, bound by its table's columns.</summary>
    private sealed record QueryCells(StackPanel Name, IReadOnlyList<string> Cells);
}
