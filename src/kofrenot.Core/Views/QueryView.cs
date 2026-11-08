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

    public IReadOnlyList<QueryRow> Rows => [.. Nodes.Select(node => new QueryRow(node, [.. definition.Columns.Select(column => CellOf(node, column))]))];

    /// <summary>A row's columns on one line, as a phone shows them: <c>written by Frank Herbert · Date 2026-03-14</c>; the empty ones left out.</summary>
    public string LineOf(QueryRow row) =>
        string.Join(" · ", Headers.Zip(row.Cells).Where(column => column.Second.Length > 0).Select(column => $"{column.First} {column.Second}"));

    /// <summary>The rows whose node's name matches <paramref name="text"/> (<see cref="GraphSearch.MatcherOf"/>), in the order shown.</summary>
    public IReadOnlyList<QueryRow> Find(string text)
    {
        var matches = GraphSearch.MatcherOf(text);
        return [.. Rows.Where(row => matches(row.Node.Name))];
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
