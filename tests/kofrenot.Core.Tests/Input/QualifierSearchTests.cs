namespace Kofrenot.Core.Tests.Input;

/// <summary>Searching links by their qualifiers, and values in a range, offline.</summary>
public sealed class QualifierSearchTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);
    private static readonly FieldDefinition Date = new("date", "Date", FieldKind.Date);
    private static readonly FieldDefinition Pages = new("pages", "Pages", FieldKind.Number);

    private readonly KnowledgeGraph _graph = new();
    private readonly Node _frank;
    private readonly Node _dune;
    private readonly Node _messiah;
    private readonly Node _ursula;
    private readonly Node _earthsea;

    public QualifierSearchTests()
    {
        _graph.Apply(new LinkTypeDefined(Guid.NewGuid(), Start, "wrote", "wrote", "written by", [Date, Pages], []));
        _frank = Add("Frank Herbert");
        _dune = Add("Dune");
        _messiah = Add("Dune Messiah");
        _ursula = Add("Ursula K. Le Guin");
        _earthsea = Add("A Wizard of Earthsea");
        Wrote(_frank, _dune, "1965-08-01", "412");
        Wrote(_frank, _messiah, "1969-10-15", "256");
        Wrote(_ursula, _earthsea, "1968-11-01", "183");
        var cited = new Link(Guid.NewGuid(), TypeCatalog.RelatesTo.Id, _dune.Id, _earthsea.Id, Start);
        _graph.Apply(new LinkAdded(Guid.NewGuid(), Start, cited));
        _graph.Apply(new FieldSet(Guid.NewGuid(), Start, cited, FieldSet.Own, "source", "Source", FieldKind.Url, "https://isfdb.org", []));
    }

    private Node Add(string name)
    {
        var node = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, name, Start);
        _graph.Add(node);
        return node;
    }

    private void Wrote(Node author, Node book, string date, string pages)
    {
        var link = new Link(Guid.NewGuid(), "wrote", author.Id, book.Id, Start);
        _graph.Apply(new LinkAdded(Guid.NewGuid(), Start, link));
        _graph.Apply(new FieldSet(Guid.NewGuid(), Start, link, "wrote", Date.Id, Date.Name, FieldKind.Date, date, []));
        _graph.Apply(new FieldSet(Guid.NewGuid(), Start, link, "wrote", Pages.Id, Pages.Name, FieldKind.Number, pages, []));
    }

    private IReadOnlyList<string> Found(string text) =>
        LinkSearch.Find(_graph, text)!.Select(hit => $"{hit.From.Name} {hit.Verb!.Name} {hit.To.Name} > {hit.Found.Name}").ToList();

    [Fact]
    public void ALinkPhrase_AndAQualifier_FindLinksOfThatTypeWithThatValue() =>
        Assert.Equal(["Dune written by Frank Herbert > Dune"], Found("written by, Date 1965"));

    [Fact]
    public void ANode_ALinkPhrase_AndARange_FindItsLinksInIt() =>
        Assert.Equal(["Frank Herbert wrote Dune > Dune", "Frank Herbert wrote Dune Messiah > Dune Messiah"], Found("Frank Herbert wrote, Date 1960..1970"));

    [Fact]
    public void ANode_WithTheOtherWay_FindsNothing() =>
        Assert.Empty(Found("Frank Herbert written by, Date 1960..1970"));

    [Fact]
    public void AQualifierAlone_FindsAnyLinkWithThatValue() =>
        Assert.Equal(["Dune relates to A Wizard of Earthsea > Dune"], Found(", Source isfdb"));

    [Fact]
    public void AQualifierWithoutValue_FindsTheLinksWithItSet() =>
        Assert.Equal(3, Found("wrote, Pages").Count);

    [Theory]
    [InlineData(", Date 1968..", 2)]
    [InlineData(", Date ..1968-11", 2)]
    [InlineData(", Date 1969-10-15..1969-10-15", 1)]
    [InlineData(", Pages 200..300", 1)]
    [InlineData(", Pages ..199", 1)]
    [InlineData(", Date 19..20", 0)]
    [InlineData(", Pages a..b", 0)]
    public void ARange_FindsTheDatesAndNumbersInIt(string text, int count) =>
        Assert.Equal(count, Found(text).Count);

    [Theory]
    [InlineData("Herbert, Frank")]
    [InlineData("Dune, Date 1965")]
    [InlineData("Dune written by Frank Herbert, Date 1965")]
    [InlineData(", Colour blue")]
    [InlineData("Arrakis wrote, Date 1965")]
    public void AnythingElse_IsNoLinkSearch(string text) =>
        Assert.Null(LinkSearch.Find(_graph, text));

    [Fact]
    public void AFieldWithoutValue_MatchesNothing() =>
        Assert.False(ValueQuery.Matches(new NodeField("wrote", "date", "Date", FieldKind.Date, [], "wrote", "Date", Values: null), string.Empty));

    [Fact]
    public void ARange_OnText_FindsNothing() =>
        Assert.Empty(Found(", Source a..b"));

    [Fact]
    public void ANodeFieldsName_AndARange_FindNodesInIt()
    {
        _graph.Apply(new FieldSet(Guid.NewGuid(), Start, _frank, FieldSet.Own, "born", "Born", FieldKind.Date, "1920-10-08", []));
        _graph.Apply(new FieldSet(Guid.NewGuid(), Start, _ursula, FieldSet.Own, "born", "Born", FieldKind.Date, "1929-10-21", []));

        var hits = GraphSearch.Find(_graph, "born 1900..1925");

        Assert.Equal(("Frank Herbert", "Born: 1920-10-08"), (Assert.Single(hits).Node.Name, hits[0].Field));
        Assert.Equal(2, GraphSearch.Find(_graph, "Born 1900..").Count);
        Assert.Empty(GraphSearch.Find(_graph, "Died 1900.."));
    }

    [Fact]
    public void TheInputBox_ListsTheLinksFound_EachOpeningTheNodeItLeadsTo()
    {
        var presenter = new InputPresenter();

        presenter.Update(_graph, "Frank Herbert wrote, Date 1960..1970");

        Assert.Null(presenter.Plan);
        Assert.Equal([("Frank Herbert wrote Dune", "Date: 1965-08-01", "Dune"), ("Frank Herbert wrote Dune Messiah", "Date: 1969-10-15", "Dune Messiah")],
            presenter.Rows.Select(row => (row.Text, row.Detail!, row.Node!.Name)));
        Assert.Equal("2 links found. Up/Down, then Enter to see the details of one.", presenter.Preview);
        Assert.True(presenter.MoveSelection(1));
        Assert.Equal(_dune, presenter.SelectedRow!.Node);
        presenter.Update(_graph, ", Source isfdb");
        Assert.Equal("1 link found. Down, then Enter to see its details.", presenter.Preview);
        presenter.Update(_graph, ", Source wikipedia");
        Assert.Equal("No link has this qualifier.", presenter.Preview);
    }
}
