using Kofrenot.Core.Browse;
using Kofrenot.Core.Fields;
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

    /// <summary>
    /// The columns this query can show: every link phrase, each way; the fields of its node type (of the types of the nodes
    /// it finds when it names none), named <c>Born (Person)</c> when two have one name; the qualifiers of its link; then
    /// any column shown but no longer among them, to take it off.
    /// </summary>
    public IReadOnlyList<ColumnChoice> ColumnChoices(KnowledgeGraph graph)
    {
        var links = graph.Types.Verbs.Select(verb => new ColumnChoice(new LinkColumn(verb.Type.Id, verb.IsReverse), verb.Name));
        var choices = new List<ColumnChoice>([.. links, .. FieldChoices(graph), .. QualifierChoices(graph)]);
        var offered = choices.Select(choice => choice.Column).ToHashSet();
        choices.AddRange(Columns.Where(column => !offered.Contains(column)).Select(column => new ColumnChoice(column, NameOf(column, Parsed?.LinkType, graph))));
        return choices;
    }

    /// <summary>The name of the qualifiers' group: <c>Qualifiers of “read by”</c>.</summary>
    public string QualifiersHeading(KnowledgeGraph graph) =>
        Parsed is null ? "Qualifiers" : $"Qualifiers of “{VerbName(Parsed.LinkType, Parsed.Reverse, graph)}”";

    private IEnumerable<ColumnChoice> FieldChoices(KnowledgeGraph graph)
    {
        if (Parsed is null)
        {
            return [];
        }

        var typeIds = Parsed.NodeType is { } type ? [type] : new QueryView(graph, Parsed).Nodes.SelectMany(node => graph.TypeIdsOf(node.Id)).Distinct();
        var fields = typeIds.Select(graph.Types.FindNodeType).OfType<NodeType>().OrderBy(found => found.Name, StringComparer.CurrentCultureIgnoreCase)
            .SelectMany(found => found.Fields.Where(IsShownInAColumn).Select(field => (Type: found, Field: field)))
            .ToList();
        var sameName = fields.GroupBy(field => NameKey.Of(field.Field.Name)).Where(names => names.Count() > 1).Select(names => names.Key).ToHashSet();
        return fields.Select(field => new ColumnChoice(
            new FieldColumn(field.Type.Id, field.Field.Id),
            sameName.Contains(NameKey.Of(field.Field.Name)) ? $"{field.Field.Name} ({field.Type.Name})" : field.Field.Name));
    }

    private IEnumerable<ColumnChoice> QualifierChoices(KnowledgeGraph graph) =>
        Parsed is null
            ? []
            : graph.LinkTypes.FieldsOf(Parsed.LinkType).Where(IsShownInAColumn).Select(field => new ColumnChoice(new QualifierColumn(field.Id), field.Name));

    // A value read as text: not an image, nor content.
    private static bool IsShownInAColumn(FieldDefinition field) => field.Kind != FieldKind.Image && !field.Kind.IsContent();

    /// <summary>
    /// A column's name: its link phrase (<c>written by</c>), its field's or its qualifier's (of <paramref name="linkType"/>,
    /// the query's); an id when this version does not know it.
    /// </summary>
    public static string NameOf(ViewColumn column, string? linkType, KnowledgeGraph graph) => column switch
    {
        LinkColumn link => VerbName(link.LinkType, link.Reverse, graph),
        FieldColumn field => graph.Types.FindNodeType(field.Owner)?.Fields.FirstOrDefault(found => found.Id == field.Field)?.Name ?? field.Field,
        QualifierColumn qualifier => (linkType is null ? null : graph.LinkTypes.FieldsOf(linkType).FirstOrDefault(found => found.Id == qualifier.Field)?.Name) ?? qualifier.Field,
        _ => string.Empty,
    };

    // A link phrase read one way (written by), or the link type's id when this version does not know it.
    private static string VerbName(string linkType, bool reverse, KnowledgeGraph graph) =>
        graph.Types.FindLinkType(linkType) is { } type ? new LinkVerb(type, reverse && type.ReversePhrases is not null).Name : linkType;

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
        var verb = VerbName(definition.LinkType, definition.Reverse, graph);
        var node = definition.Node is { } id && graph.FindNode(id) is { } found ? $" {found.Name}" : string.Empty;
        var fields = graph.LinkTypes.FieldsOf(definition.LinkType);
        var qualifiers = definition.Qualifiers.Select(qualifier =>
            $", {fields.FirstOrDefault(field => field.Id == qualifier.Field)?.Name ?? qualifier.Field}{(qualifier.Value.Length > 0 ? $" {qualifier.Value}" : string.Empty)}");
        return $"{QueryParser.Mark}{type}{verb}{node}{string.Concat(qualifiers)}";
    }
}

/// <summary>A column a query view can show, with the name its box shows.</summary>
public sealed record ColumnChoice(ViewColumn Column, string Name);
