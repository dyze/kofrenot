using Avalonia.Controls;
using Avalonia.Threading;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Input;
using Kofrenot.Core.Storage;

namespace Kofrenot.Phone.Views;

/// <summary>
/// The home (the search tab, its box empty) fits the screen, as on Windows: as many recent nodes as its
/// height holds, in columns when it is wide (<see cref="HomeRoom"/>, <see cref="HomeColumns"/>,
/// <see cref="HomeColumnsPanel"/>): one on a phone held upright, two turned, three on a tablet.
/// Measured from the rows shown, once laid out.
/// </summary>
public sealed partial class HomePage
{
    // Until a row is laid out; then the tallest seen, so that the fit settles.
    private double _rowHeight = PhoneTheme.Size("RowHeight");
    private double _headerHeight = PhoneTheme.Size("RowHeight") * 0.75;
    private HomeRoom? _room;

    // The height ＋ takes at the bottom right, with its margins (App.axaml, Button.fab).
    private const double FabCorner = 72;

    private bool ShowsHome => _tab.IsSearch && Chips.Text.Length == 0;

    // Pinned, then recent (opened on this phone or changed on any device), as the input box on Windows.
    private void ShowHome(Vault vault)
    {
        var graph = vault.Graph;
        var start = StartNodes.Of(graph, _shell.Services.Recent.Of(vault.Id), DateTimeOffset.UtcNow, _room?.MaxRows ?? StartNodes.MaxRecent);
        var rows = new List<object>();
        AddSection(rows, "Pinned", start.Pinned.Select(node => NodeRow.Of(graph, node)));
        var pinned = rows.Count;
        AddSection(rows, "Recent", start.Recent.Select(recent => NodeRow.Of(graph, recent.Node, detail: recent.Detail)));
        var columns = _room is { } room ? HomeColumns.Of(rows.Select(row => row is HeaderRow).ToList(), room, kept: pinned) : new HomeColumns([0], rows.Count);
        HomeColumnsPanel.SetStarts(List, columns.Starts);
        UseItemsPanel(HomePanel);
        List.ItemsSource = rows.Take(columns.Shown).ToList();
        ShowMessage(rows.Count == 0 ? "Type a name to find a node, or what to add: Dune relates to Frank Herbert. Pinned and recent nodes show here." : null);
        Dispatcher.UIThread.Post(FitHome, DispatcherPriority.Loaded);
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

    private void List_SizeChanged(object? sender, SizeChangedEventArgs e) => FitHome();

    // The room the list has, the whole of its height (the rows beside ＋ narrower); the home shown
    // again when it holds another number of rows.
    private void FitHome()
    {
        var height = List.Bounds.Height;
        if (!ShowsHome || Vault is not { } vault || List.Bounds.Width <= 0 || height <= 0)
        {
            return;
        }

        HomeColumnsPanel.SetCornerTop(List, height - FabCorner);
        MeasureRows();
        var room = new HomeRoom(List.Bounds.Width, height, _rowHeight, _headerHeight);
        if (room == _room)
        {
            return;
        }

        _room = room;
        ShowHome(vault);
    }

    private void MeasureRows()
    {
        for (var index = 0; index < List.ItemCount; index++)
        {
            if (List.ContainerFromIndex(index) is { Bounds.Height: > 0 and var height })
            {
                if (List.Items[index] is HeaderRow)
                {
                    _headerHeight = Math.Max(_headerHeight, height);
                }
                else
                {
                    _rowHeight = Math.Max(_rowHeight, height);
                }
            }
        }
    }
}
