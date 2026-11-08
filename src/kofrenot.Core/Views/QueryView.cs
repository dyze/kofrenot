using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.Core.Views;

/// <summary>A node a query view found, with what each of its columns shows: the names of the nodes linked so, joined.</summary>
public sealed record QueryRow(Node Node, IReadOnlyList<string> Cells);

/// <summary>
/// A query view: the nodes its query finds (<see cref="QueryDefinition"/>), by name, each with its columns. Read from the
/// graph each time, so it shows changes.
/// </summary>
public sealed class QueryView(KnowledgeGraph graph, QueryDefinition definition)
{
    /// <summary>The node the query names is not here: removed, or not synced yet; nothing is found.</summary>
    public bool IsBroken => definition.Node is { } node && graph.FindNode(node) is null;

    /// <summary>Each column's header: its link phrase, or the link type's id when this version does not know it.</summary>
    public IReadOnlyList<string> Headers => [.. definition.Columns.Select(column => QueryDraft.NameOf(column, graph))];

    public IReadOnlyList<QueryRow> Rows
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

            return [.. NodeOrder.ByName(nodes).Select(node => new QueryRow(node, [.. definition.Columns.Select(column => CellOf(node, column))]))];
        }
    }

    /// <summary>A row's columns on one line, as a phone shows them: <c>written by Frank Herbert · contains Arrakis</c>; the empty ones left out.</summary>
    public string LineOf(QueryRow row) =>
        string.Join(" · ", Headers.Zip(row.Cells).Where(column => column.Second.Length > 0).Select(column => $"{column.First} {column.Second}"));

    /// <summary>The rows whose node's name matches <paramref name="text"/> (<see cref="GraphSearch.MatcherOf"/>), in the order shown.</summary>
    public IReadOnlyList<QueryRow> Find(string text)
    {
        var matches = GraphSearch.MatcherOf(text);
        return [.. Rows.Where(row => matches(row.Node.Name))];
    }

    // The nodes the node's links of the column's type lead to, read its way, by name.
    private string CellOf(Node node, ViewColumn column) =>
        string.Join(", ", NodeOrder.ByName(graph.LinksOf(node.Id)
                .Where(link => graph.Types.VerbOf(link, node.Id) is { } verb && verb.Type.Id == column.LinkType && verb.IsReverse == column.Reverse)
                .Select(link => link.Other(node.Id))
                .Where(other => other != node.Id)
                .Distinct()
                .Select(graph.FindNode)
                .OfType<Node>())
            .Select(other => other.Name));
}
