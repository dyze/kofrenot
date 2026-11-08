using System.Windows;
using System.Windows.Controls;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>
/// A query view (<see cref="QueryView"/>): the nodes its query finds, in the list, each row with its columns (the names of
/// the nodes its links lead to: <i>written by</i> → <i>Frank Herbert</i>), under a header naming them.
/// </summary>
public partial class ViewPanel
{
    private const double MinColumnWidth = 120;

    private QueryView? _query;

    private void ShowQuery(KnowledgeGraph graph, QueryDefinition definition)
    {
        _query = new QueryView(graph, definition);
        if (_query.IsBroken)
        {
            ShowMessage("The node this query names is not there: removed, or not synced yet.");
            return;
        }

        var headers = _query.Headers;
        FillQueryHeader(headers);
        UseCardsPanel(false);
        var rows = _query.Rows.Select(row => QueryRowOf(row, headers.Count)).ToList();
        List.Items.Clear();
        rows.ForEach(row => List.Items.Add(row));
        List.SelectedItem = rows.FirstOrDefault(row => NodeOf(row)?.Id == _selectedNode);
        ShowOnly(List);
        if (rows.Count == 0)
        {
            ShowMessage($"No node found by “{QueryDraft.TextOf(definition, graph)}”.");
        }
    }

    /// <summary>The header over the rows: Name, then each column's link phrase; shown while the list is.</summary>
    private void FillQueryHeader(IReadOnlyList<string> headers)
    {
        QueryHeader.Children.Clear();
        QueryHeader.ColumnDefinitions.Clear();
        AddColumns(QueryHeader, headers.Count);
        QueryHeader.Children.Add(new TextBlock { Text = "Name", Style = (Style)FindResource("SecondaryText") });
        for (var index = 0; index < headers.Count; index++)
        {
            var header = new TextBlock { Text = headers[index], Style = (Style)FindResource("SecondaryText"), TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(header, index + 1);
            QueryHeader.Children.Add(header);
        }
    }

    // The node pictured with its name, as a list's row (so renaming finds it), then a cell per column.
    private ListBoxItem QueryRowOf(QueryRow row, int columns)
    {
        var grid = new Grid();
        AddColumns(grid, columns);
        grid.Children.Add(NamePanelOf(row.Node));
        for (var index = 0; index < row.Cells.Count; index++)
        {
            AddCell(grid, index + 1, row.Cells[index]);
        }

        return new ListBoxItem { Content = grid, Tag = row.Node, Style = (Style)FindResource("ListRowItem") };
    }

    // The name as wide as two columns, each column at least wide enough to read a name.
    private static void AddColumns(Grid grid, int columns)
    {
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MinWidth = MinColumnWidth });
        for (var index = 0; index < columns; index++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = MinColumnWidth });
        }
    }
}
