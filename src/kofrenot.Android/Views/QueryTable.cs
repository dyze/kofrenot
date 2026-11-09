using Avalonia;
using Avalonia.Controls;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;

namespace Kofrenot.Phone.Views;

/// <summary>A header of a query view's table: a tap sorts by its <see cref="Column"/> (<see cref="QuerySort"/>).</summary>
public sealed record QueryColumnHeader(string Text, double Width, int Column);

/// <summary>A cell of a query view's table, as wide as its column.</summary>
public sealed record QueryCell(string Text, double Width);

/// <summary>A row of a query view's table: its node's row (pictured, named) as wide as Name, then its cells.</summary>
public sealed record QueryTableRow(NodeRow Row, double NameWidth, IReadOnlyList<QueryCell> Cells);

/// <summary>
/// A query view as a table, as on Windows: Name, then a column per header, each as wide as its widest text (at most
/// <see cref="MaxWidth"/>, longer text cut), so the header and the rows line up; the column sorted by marked ▲ or ▼.
/// </summary>
public sealed class QueryTable
{
    private const double MaxWidth = 200;

    // A cell's text: 8 dp on each side.
    private const double CellPadding = 16;

    // Before and after a node's name: its row's margins, picture and gap (the NodeRow template, App.axaml).
    private const double NameRowExtra = 8 + 28 + 8 + 8;

    public QueryTable(KnowledgeGraph graph, QueryView query, IReadOnlyList<QueryRow> rows, QuerySort sort)
    {
        var titles = query.Headers.Prepend("Name").Select((title, column) => column == sort.Column ? $"{title} {(sort.Descending ? "▼" : "▲")}" : title).ToList();
        var nameWidth = Fit(Math.Max(TextWidth(titles[0], PhoneTheme.Size("CaptionSize")) + CellPadding, rows.Select(row => TextWidth(row.Node.Name) + NameRowExtra).DefaultIfEmpty().Max()));
        var cellWidths = query.Headers.Select((_, cell) => Fit(Math.Max(TextWidth(titles[cell + 1], PhoneTheme.Size("CaptionSize")), rows.Select(row => TextWidth(row.Cells[cell])).DefaultIfEmpty().Max()) + CellPadding)).ToList();
        Header = [.. titles.Select((title, column) => new QueryColumnHeader(title, column == QuerySort.NameColumn ? nameWidth : cellWidths[column - 1], column))];
        Rows = [.. rows.Select(row => new QueryTableRow(NodeRow.Of(graph, row.Node), nameWidth, [.. row.Cells.Zip(cellWidths, (text, width) => new QueryCell(text, width))]))];
    }

    public IReadOnlyList<QueryColumnHeader> Header { get; }

    public IReadOnlyList<QueryTableRow> Rows { get; }

    private static double Fit(double width) => Math.Ceiling(Math.Min(width, MaxWidth));

    private static double TextWidth(string text) => TextWidth(text, PhoneTheme.Size("BodySize"));

    private static double TextWidth(string text, double fontSize)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var block = new TextBlock { Text = text, FontSize = fontSize };
        block.Measure(Size.Infinity);
        return block.DesiredSize.Width;
    }
}
