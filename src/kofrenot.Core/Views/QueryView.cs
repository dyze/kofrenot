using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.Core.Views;

/// <summary>A node a query view found, with what each of its columns shows, as text.</summary>
public sealed record QueryRow(Node Node, IReadOnlyList<string> Cells);

/// <summary>
/// A query view: the nodes its query finds (<see cref="QueryDefinition"/>), by name, each with its columns. Read from the
/// graph each time, so it shows changes.
/// </summary>
public sealed class QueryView(KnowledgeGraph graph, QueryDefinition definition)
{
    /// <summary>The node the query names is not here: removed, or not synced yet; nothing is found.</summary>
    public bool IsBroken => definition.Node is { } node && graph.FindNode(node) is null;

    /// <summary>Each column's header (<see cref="QueryDraft.NameOf"/>).</summary>
    public IReadOnlyList<string> Headers => [.. definition.Columns.Select(column => QueryDraft.NameOf(column, definition.LinkType, graph))];

    /// <summary>The nodes found, by name, before their columns are read.</summary>
    public IReadOnlyList<Node> Nodes
    {
        get
        {
            if (IsBroken)
            {
                return [];
            }

            IReadOnlyList<Node>? targets = definition.Node is { } id ? [graph.FindNode(id)!] : null;
            var nodes = NodeQuerySearch.Linked(graph, definition.LinkType, definition.Reverse, targets, definition.Qualifiers);
            if (definition.NodeType is { } type)
            {
                nodes = nodes.Where(node => graph.HasType(node.Id, type));
            }

            return [.. NodeOrder.ByName(nodes)];
        }
    }

    /// <summary>The rows by name.</summary>
    public IReadOnlyList<QueryRow> Rows => [.. Nodes.Select(node => new QueryRow(node, [.. definition.Columns.Select(column => CellOf(node, column))]))];

    /// <summary>The rows in <paramref name="sort"/>'s order: by name when its column is not here (the view was edited); empty cells last; ties by name.</summary>
    public IReadOnlyList<QueryRow> RowsBy(QuerySort sort)
    {
        var rows = Rows;
        if (sort.Column == QuerySort.NameColumn)
        {
            return sort.Descending ? [.. rows.Reverse()] : rows;
        }

        if (sort.Column < 1 || sort.Column > definition.Columns.Count)
        {
            return rows;
        }

        var cell = sort.Column - 1;
        var filled = rows.Where(row => row.Cells[cell].Length > 0);
        var comparer = Comparer<string>.Create(QuerySort.CompareCells);
        var sorted = sort.Descending ? filled.OrderByDescending(row => row.Cells[cell], comparer) : filled.OrderBy(row => row.Cells[cell], comparer);
        return [.. sorted, .. rows.Where(row => row.Cells[cell].Length == 0)];
    }

    /// <summary>The rows whose node's name matches <paramref name="text"/> (<see cref="GraphSearch.MatcherOf"/>), in <paramref name="sort"/>'s order (by name when none).</summary>
    public IReadOnlyList<QueryRow> Find(string text, QuerySort? sort = null)
    {
        var matches = GraphSearch.MatcherOf(text);
        return [.. RowsBy(sort ?? QuerySort.ByName).Where(row => matches(row.Node.Name))];
    }

    private string CellOf(Node node, ViewColumn column) => column switch
    {
        LinkColumn link => LinkedNames(node, link),
        FieldColumn field => NodeFields.Of(graph, node.Id).FirstOrDefault(shown => shown.Owner == field.Owner && shown.Field == field.Field)?.Display ?? string.Empty,
        QualifierColumn qualifier => QualifierValues(node, qualifier),
        _ => string.Empty,
    };

    // The nodes the node's links of the column's type lead to, read its way, by name.
    private string LinkedNames(Node node, LinkColumn column) =>
        string.Join(", ", NodeOrder.ByName(graph.LinksOf(node.Id)
                .Where(link => Reads(link, node.Id, column.LinkType, column.Reverse))
                .Select(link => link.Other(node.Id))
                .Where(other => other != node.Id)
                .Distinct()
                .Select(graph.FindNode)
                .OfType<Node>())
            .Select(other => other.Name));

    // The qualifier's values on the links the query found the node through (a book read twice: both dates), in order.
    private string QualifierValues(Node node, QualifierColumn column) =>
        string.Join(", ", graph.LinksOf(node.Id)
            .Where(link => Reads(link, node.Id, definition.LinkType, definition.Reverse)
                && link.Other(node.Id) != node.Id
                && (definition.Node is not { } target || link.Other(node.Id) == target)
                && NodeQuerySearch.HasQualifiers(graph, link, definition.Qualifiers))
            .Select(link => NodeFields.OfLink(graph, link).FirstOrDefault(field => field.Field == column.Field))
            .Where(field => field is { Value: not null })
            .OrderBy(field => field!.Value, StringComparer.Ordinal)
            .Select(field => field!.Display)
            .Distinct());

    private bool Reads(Link link, Guid from, string linkType, bool reverse) =>
        graph.Types.VerbOf(link, from) is { } verb && verb.Type.Id == linkType && verb.IsReverse == reverse;
}
