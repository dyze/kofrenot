using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Views;

namespace Kofrenot.Phone.Views;

/// <summary>
/// A query view's tab: a table (<see cref="QueryTable"/>) under a header that stays, the whole scrolled sideways when
/// wider than the screen. A tap on a header sorts by it, again the other way, kept per view on this phone (<see cref="Kofrenot.Core.Browse.ViewStates"/>).
/// </summary>
public sealed partial class HomePage
{
    private List<object> QueryRows(KnowledgeGraph graph, Node view, QueryDefinition definition, string text)
    {
        var query = new QueryView(graph, definition);
        var sort = Vault is { } vault ? _shell.Services.ViewStates.SortOf(vault.Id, view.Id) : QuerySort.ByName;
        var rows = text.Length > 0 ? query.Find(text, sort) : query.RowsBy(sort);
        ShowMessage(query.IsBroken ? "The node this query names is not there: removed, or not synced yet."
            : rows.Count == 0 ? (text.Length > 0 ? "No node of that name." : $"No node found by “{QueryDraft.TextOf(definition, graph)}”.")
            : null);
        if (query.IsBroken)
        {
            return [];
        }

        var table = new QueryTable(graph, query, rows, sort);
        ShowTableHeader(table.Header);
        return [.. table.Rows];
    }

    // The header over a query view's rows, and the sideways scroll; none for the other tabs.
    private void ShowTableHeader(IReadOnlyList<QueryColumnHeader>? header)
    {
        TableHeader.ItemsSource = header;
        TableHeader.IsVisible = header is not null;
        TableScroll.HorizontalScrollBarVisibility = header is null ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        if (header is null)
        {
            TableScroll.Offset = default;
        }
    }

    private void QueryHeader_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is QueryColumnHeader header && Vault is { } vault && _tab.View is { } view)
        {
            var states = _shell.Services.ViewStates;
            states.SetSort(vault.Id, view.Id, states.SortOf(vault.Id, view.Id).ClickedOn(header.Column));
            _shell.Services.Log.Info("Action", "Query view sorted");
            ShowList(vault);
        }
    }
}
