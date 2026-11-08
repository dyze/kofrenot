using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.Core.Views;

/// <summary>
/// A query view's definition as edited: the query as typed (<see cref="Text"/>), what it reads as (<see cref="Parsed"/>,
/// or the <see cref="Problem"/> that stops it), and the columns shown. The node it names is kept by id, so a
/// rename does not change what it finds; its text is written again from the names of the day (<see cref="TextOf"/>).
/// </summary>
public sealed record QueryDraft(string Text, QueryDefinition? Parsed, string? Problem, IReadOnlyList<ViewColumn> Columns)
{
    public const string Hint = "A query view lists the nodes linked to a node: ?book: read by Alice, Date 2026.";

    public static QueryDraft Default { get; } = new(string.Empty, null, Hint, []);

    /// <summary>The definition, or null while the query reads as none.</summary>
    public QueryDefinition? Definition => Parsed is null ? null : Parsed with { Columns = Columns };

    /// <summary>A saved definition, to edit it.</summary>
    public static QueryDraft Of(QueryDefinition definition, KnowledgeGraph graph) =>
        new(TextOf(definition, graph), definition with { Columns = [] }, null, definition.Columns);

    /// <summary>What the editor says under the query: its problem, or how many nodes it finds.</summary>
    public string StatusIn(KnowledgeGraph graph) => Problem ?? (Parsed is null ? Hint : new QueryView(graph, Parsed).Rows.Count switch
    {
        0 => "No node found.",
        1 => "1 node found.",
        var count => $"{count} nodes found.",
    });

    /// <summary>The query typed, read again; the columns stay.</summary>
    public QueryDraft WithText(string text, KnowledgeGraph graph)
    {
        var (parsed, problem) = Read(text, graph);
        return this with { Text = text, Parsed = parsed, Problem = problem };
    }

    /// <summary>Shows a column, after the others, or no longer shows it.</summary>
    public QueryDraft WithColumn(ViewColumn column, bool isShown) => this with
    {
        Columns = isShown ? [.. Columns.Where(shown => shown != column), column] : [.. Columns.Where(shown => shown != column)],
    };

    /// <summary>The columns a query view can show: every link phrase, each way.</summary>
    public static IReadOnlyList<ViewColumn> ColumnChoices(KnowledgeGraph graph) =>
        [.. graph.Types.Verbs.Select(verb => new ViewColumn(verb.Type.Id, verb.IsReverse))];

    /// <summary>A column's name: its link phrase (<c>written by</c>), or the link type's id when this version does not know it.</summary>
    public static string NameOf(ViewColumn column, KnowledgeGraph graph) =>
        graph.Types.FindLinkType(column.LinkType) is { } type ? new LinkVerb(type, column.Reverse && type.ReversePhrases is not null).Name : column.LinkType;

    /// <summary>
    /// The definition a query reads as (<c>?</c> optional), with no column; or why it is none: no link phrase, a time,
    /// or a node named by no node, or by several.
    /// </summary>
    public static (QueryDefinition? Definition, string? Problem) Read(string text, KnowledgeGraph graph)
    {
        var query = QueryParser.Parse(QueryParser.IsQuery(text) ? text : $"{QueryParser.Mark}{text}", graph);
        if (query is not { Link: { } link, Time: null })
        {
            return (null, Hint);
        }

        Guid? node = null;
        if (link.Node.Length > 0)
        {
            switch (graph.NodesNamed(link.Node))
            {
                case [var named]:
                    node = named.Id;
                    break;
                case []:
                    return (null, $"No node is named “{link.Node}”.");
                case var named:
                    return (null, $"{named.Count} nodes are named “{link.Node}”: rename one to use it here.");
            }
        }

        return (new QueryDefinition(query.Type?.Id, link.Verb.Type.Id, link.Verb.IsReverse, node, link.Qualifiers, []), null);
    }

    /// <summary>
    /// The query a definition reads as, with today's names: <c>?book: read by Alice, Date 2026</c>. A type this version does not
    /// know is written by its id; a node not here is left out.
    /// </summary>
    public static string TextOf(QueryDefinition definition, KnowledgeGraph graph)
    {
        var type = definition.NodeType is { } nodeType ? $"{graph.Types.NodeTypeName(nodeType)}: " : string.Empty;
        var verb = NameOf(new ViewColumn(definition.LinkType, definition.Reverse), graph);
        var node = definition.Node is { } id && graph.FindNode(id) is { } found ? $" {found.Name}" : string.Empty;
        var fields = graph.LinkTypes.FieldsOf(definition.LinkType);
        var qualifiers = definition.Qualifiers.Select(qualifier =>
            $", {fields.FirstOrDefault(field => field.Id == qualifier.Field)?.Name ?? qualifier.Field}{(qualifier.Value.Length > 0 ? $" {qualifier.Value}" : string.Empty)}");
        return $"{QueryParser.Mark}{type}{verb}{node}{string.Concat(qualifiers)}";
    }
}
