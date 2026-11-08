namespace Kofrenot.Core.Tests.Input;

/// <summary>Queries through links whose qualifiers match: <c>?book: read by Alice, Date 2026</c>.</summary>
public sealed class QueryQualifierTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 9, 0, 0, TimeSpan.Zero);
    private static readonly FieldDefinition Date = new("date", "Date", FieldKind.Date);
    private static readonly FieldDefinition Format = new("format", "Format", FieldKind.Text);

    private readonly KnowledgeGraph _graph = new();
    private readonly LinkVerb _readBy;
    private readonly Node _alice;
    private readonly Node _dune;
    private readonly Node _foundation;
    private readonly Node _earthsea;

    public QueryQualifierTests()
    {
        _graph.Apply(new LinkTypeDefined(Guid.NewGuid(), Now, "read", "read", "read by", [Date, Format], []));
        _readBy = new LinkVerb(_graph.Types.FindLinkType("read")!, IsReverse: true);
        _alice = Add("Alice");
        _dune = Add("Dune");
        _foundation = Add("Foundation");
        _earthsea = Add("Earthsea");
        Read(_alice, _dune, "2026-03-14", "paper");
        Read(_alice, _foundation, "2025-07-01", "ebook");
        Read(Add("Bob"), _earthsea, "2026-01-02", null);
    }

    [Fact]
    public void AQualifier_AfterTheNode_IsReadWithItsFieldId() =>
        Assert.Equal(
            new NodeQuery(null, "", new LinkQuery(_readBy, "Alice") { Qualifiers = [new("date", "2026")] }, null),
            QueryParser.Parse("?read by  Alice, date 2026", _graph));

    [Fact]
    public void SeveralQualifiers_AndNoNode_AreRead() =>
        Assert.Equal(
            [new QualifierFilter("date", "2026-01..2026-06"), new QualifierFilter("format", "")],
            QueryParser.Parse("?read by, Date 2026-01..2026-06, Format", _graph)!.Link!.Qualifiers);

    [Fact]
    public void ACommaNotFollowedByAQualifier_StaysInTheName()
    {
        var link = QueryParser.Parse("?read by Herbert, Frank", _graph)!.Link!;

        Assert.Equal(("Herbert, Frank", 0), (link.Node, link.Qualifiers.Count));
    }

    [Fact]
    public void WithoutTheLinkTypesQualifiers_NoneIsRead() =>
        Assert.Equal("Alice, Date 2026", QueryParser.Parse("?read by Alice, Date 2026", _graph.Types)!.Link!.Node);

    [Fact]
    public void TheNodesFound_AreLinkedByALinkWithThoseValues()
    {
        Assert.Equal([_dune], Find("read by Alice, Date 2026"));
        Assert.Equal([_dune, _earthsea], Find("read by, Date 2026"));
        Assert.Equal([_dune, _foundation], Find("read by Alice, Date 2025..2026"));
        Assert.Equal([_foundation], Find("read by Alice, Format ebook"));
        Assert.Equal([_dune, _foundation], Find("read by, Format"));
        Assert.Empty(Find("read by Alice, Date 2026, Format ebook"));
    }

    [Theory]
    [InlineData("?read by Alice, Da", "te ")]
    [InlineData("?read by, F", "ormat ")]
    public void AfterAComma_TheLinkTypesQualifiersAreProposed(string text, string rest)
    {
        var proposal = QueryCompletion.Proposals(_graph, text).Single();

        Assert.Equal((rest, true), (proposal.Rest, proposal.IsQueryText));
    }

    [Fact]
    public void AfterAQualifiersValue_NothingIsProposed() =>
        Assert.Empty(QueryCompletion.Proposals(_graph, "?read by Alice, Date 20"));

    private List<Node> Find(string text) =>
        [.. NodeQuerySearch.Find(_graph, QueryParser.Parse($"?{text}", _graph)!, [], Now, TimeZoneInfo.Utc).Hits.Select(hit => hit.Node)];

    private Node Add(string name)
    {
        var node = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, name, Now);
        _graph.Add(node);
        return node;
    }

    private void Read(Node reader, Node book, string date, string? format)
    {
        var link = new Link(Guid.NewGuid(), "read", reader.Id, book.Id, Now);
        _graph.Apply(new LinkAdded(Guid.NewGuid(), Now, link));
        _graph.Apply(new FieldSet(Guid.NewGuid(), Now, link, "read", Date.Id, Date.Name, FieldKind.Date, date, []));
        if (format is not null)
        {
            _graph.Apply(new FieldSet(Guid.NewGuid(), Now, link, "read", Format.Id, Format.Name, FieldKind.Text, format, []));
        }
    }
}
