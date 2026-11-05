namespace Kofrenot.Core.Tests.Browse;

/// <summary>Where a link's qualifier shows: its row in the details, its line in a graph, a tree's sort choices.</summary>
public sealed class QualifierShownTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);
    private static readonly FieldDefinition Date = new("date", "Date", FieldKind.Date);

    private readonly KnowledgeGraph _graph = new();
    private readonly Node _ann = new(Guid.NewGuid(), TypeCatalog.Generic.Id, "Ann", Start);
    private readonly Node _bob = new(Guid.NewGuid(), TypeCatalog.Generic.Id, "Bob", Start);
    private readonly Link _first;
    private readonly Link _second;

    public QualifierShownTests()
    {
        _graph.Add(_ann);
        _graph.Add(_bob);
        _graph.Apply(new LinkTypeDefined(Guid.NewGuid(), Start, "married-to", "married to", null, [Date], []));
        _first = Married(minutes: 1, "1995-06-01");
        _second = Married(minutes: 2, "2015-06-01", separate: true);
    }

    private Link Married(int minutes, string date, bool separate = false)
    {
        var link = new Link(Guid.NewGuid(), "married-to", _ann.Id, _bob.Id, Start.AddMinutes(minutes), separate);
        _graph.Apply(new LinkAdded(Guid.NewGuid(), Start, link));
        _graph.Apply(new FieldSet(Guid.NewGuid(), Start, link, "married-to", Date.Id, Date.Name, FieldKind.Date, date, []));
        return link;
    }

    [Fact]
    public void ALinkRow_ShowsItsQualifier_AndTwoSeparateLinksAreSelectedApart()
    {
        var browser = new NodeBrowser();
        browser.Open(_graph, _ann);

        var rows = browser.Details!.Links;

        Assert.Equal([" · 1995-06-01", " · 2015-06-01"], rows.Select(row => row.QualifierSuffix));
        Assert.True(browser.Select(rows[1]));
        Assert.True(browser.Details!.Links[1].IsSelected);
        Assert.Equal(string.Empty, (rows[0] with { Qualifier = null }).QualifierSuffix);
        Assert.All(rows, row => Assert.True(row.HasQualifiers));
    }

    [Fact]
    public void ALinkRow_WithoutQualifiers_HasNoneToUnfold()
    {
        _graph.Apply(new LinkAdded(Guid.NewGuid(), Start, new Link(Guid.NewGuid(), TypeCatalog.RelatesTo.Id, _ann.Id, _bob.Id, Start)));
        var browser = new NodeBrowser();
        browser.Open(_graph, _ann);

        var related = browser.Details!.Links.Single(row => row.Link.Type == TypeCatalog.RelatesTo.Id);

        Assert.False(related.HasQualifiers);
        Assert.Null(related.Qualifier);
    }

    [Fact]
    public void AGraphLine_ShowsItsQualifierBesideItsName()
    {
        var view = new GraphView(_graph, new GraphDefinition(_ann.Id, 1, GraphLayoutKind.Network, [new GraphLink("married-to", GraphRole.Beside)], [], []));

        var edges = view.Read()!.Edges;

        Assert.Equal(["married to · 1995-06-01", "married to · 2015-06-01"], edges.Select(edge => edge.Label).Order());
        Assert.Equal("married to", (edges[0] with { Qualifier = null }).Label);
    }

    [Fact]
    public void TheCreationScreen_OffersATreesSortChoices()
    {
        var form = CreationForm.NewNode(_graph, TypeCatalog.View);

        Assert.Equal([Date], form.OrderChoicesOf(ViewDraft.Default with { LinkType = _graph.Types.FindLinkType("married-to")! }));
        Assert.Empty(form.OrderChoicesOf(ViewDraft.Default));
    }
}
