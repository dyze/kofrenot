using System.Text;

namespace Kofrenot.Core.Tests.Imports;

/// <summary>A vault with a Book type (ISBN, Original title, Published, Publisher, Cover), a Person type, and "wrote" (written by).</summary>
internal sealed class BookVault
{
    public static readonly DateTimeOffset Start = new(2026, 11, 8, 9, 0, 0, TimeSpan.Zero);

    public static readonly FieldDefinition Isbn = new("isbn", "ISBN", FieldKind.Text);
    public static readonly FieldDefinition OriginalTitle = new("original-title", "Original title", FieldKind.Text);
    public static readonly FieldDefinition Published = new("published", "Published", FieldKind.Date);
    public static readonly FieldDefinition Publisher = new("publisher", "Publisher", FieldKind.Text);
    public static readonly FieldDefinition Cover = new("cover", "Cover", FieldKind.Image);

    /// <summary>title_fr, else title → name; title → Original title; isbn, year, publisher.name; authors → Person, who wrote the book.</summary>
    public static readonly MappingDefinition Books = new(
        MappingDefinition.Json,
        "book",
        ["title_fr", "title"],
        Isbn.Id,
        [OriginalTitle.Id],
        [new FieldRule("title", OriginalTitle.Id), new FieldRule("isbn", Isbn.Id), new FieldRule("year", Published.Id), new FieldRule("publisher.name", Publisher.Id)],
        [new LinkRule("authors", "person", "wrote", IsReverse: true)]);

    public BookVault()
    {
        Graph.Apply(new NodeTypeDefined(Guid.NewGuid(), Start, "book", "Book", null, null, [Isbn, OriginalTitle, Published, Publisher, Cover], []));
        Graph.Apply(new NodeTypeDefined(Guid.NewGuid(), Start, "person", "Person", null, null, [], []));
        Graph.Apply(new LinkTypeDefined(Guid.NewGuid(), Start, "wrote", "wrote", "written by", [], []));
    }

    public KnowledgeGraph Graph { get; } = new();

    public Node Add(string name, string type = "book")
    {
        var node = new Node(Guid.NewGuid(), type, name, Start);
        Graph.Apply(new NodeAdded(Guid.NewGuid(), Start, node));
        return node;
    }

    public FieldSet Set(Node node, FieldDefinition field, string value, params Guid[] replaces)
    {
        var set = new FieldSet(Guid.NewGuid(), Start, node, "book", field.Id, field.Name, field.Kind, value, replaces);
        Graph.Apply(set);
        return set;
    }

    public void Link(Node author, Node book) =>
        Graph.Apply(new LinkAdded(Guid.NewGuid(), Start, new Link(Guid.NewGuid(), "wrote", author.Id, book.Id, Start)));

    public string? ValueOf(Node node, FieldDefinition field) =>
        Graph.FieldsOf(node.Id).FirstOrDefault(values => values.Field == field.Id)?.Shown.Value;

    public static RecordFile Read(string json) => JsonRecordReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(json)));

    public RecordPlan Plan(string json, MappingDefinition? mapping = null) => new ImportPlanner(Graph).Plan(mapping ?? Books, Read(json));

    /// <summary>Writes a plan as chosen (every record imported by default), and returns its changes.</summary>
    public IReadOnlyList<Change> Import(RecordPlan plan, Func<RecordItem, RecordChoice>? choose = null)
    {
        var changes = plan.ChangesFor(choose ?? (_ => RecordChoice.Import), Start.AddHours(1));
        foreach (var change in changes)
        {
            Graph.Apply(change);
        }

        return changes;
    }
}
