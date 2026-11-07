namespace Kofrenot.Core.Tests.Input;

/// <summary>Qualifiers typed in the input box: read after a link sentence, added with a new link, or updating an existing one.</summary>
public sealed class QualifierInputTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);
    private static readonly FieldDefinition Date = new("date", "Date", FieldKind.Date);
    private static readonly FieldDefinition Source = new("source", "Source", FieldKind.Url);
    private static readonly FieldDefinition StartDate = new("start", "Start date", FieldKind.Date);
    private static readonly FieldDefinition Place = new("place", "Place", FieldKind.Address);

    private readonly ManualClock _clock = new() { Now = Start };
    private readonly KnowledgeGraph _graph = new();
    private readonly Node _dune;
    private readonly Node _frank;

    public QualifierInputTests()
    {
        _graph.Apply(new LinkTypeDefined(Guid.NewGuid(), Start, "wrote", "wrote", "written by", [Date, Source, StartDate, Place], []));
        _dune = Add("Dune");
        _frank = Add("Frank Herbert");
    }

    private Node Add(string name)
    {
        var node = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, name, Start);
        _graph.Add(node);
        return node;
    }

    private Link Link(Node from, Node to, bool separate = false, string? date = null)
    {
        var link = new Link(Guid.NewGuid(), "wrote", from.Id, to.Id, Start.AddMinutes(_graph.Links.Count), separate);
        _graph.Apply(new LinkAdded(Guid.NewGuid(), Start, link));
        if (date is not null)
        {
            _graph.Apply(new FieldSet(Guid.NewGuid(), Start, link, "wrote", Date.Id, Date.Name, FieldKind.Date, date, []));
        }

        return link;
    }

    private LinkRequest Parse(string text) => Assert.IsType<LinkRequest>(InputParser.Parse(text, _graph));

    private LinkPlan Plan(string text, AddChoices? choices = null) => (LinkPlan)new AddPlanner(_graph, _clock).Plan(Parse(text), choices);

    private void Apply(AddPlan plan)
    {
        foreach (var change in plan.Changes)
        {
            _graph.Apply(change);
        }
    }

    [Fact]
    public void Qualifiers_AreReadAfterTheSecondNode()
    {
        var request = Parse("Dune written by Frank Herbert, Date 1965-08-01, source isfdb.org");

        Assert.Equal("Frank Herbert", request.Right);
        Assert.Equal([new QualifierInput("Date", "1965-08-01"), new QualifierInput("Source", "isfdb.org")], request.QualifiersTyped);
    }

    [Theory]
    [InlineData("Dune written by Herbert, Frank", "Herbert, Frank")]
    [InlineData("Dune written by Frank Herbert, Dated", "Frank Herbert, Dated")]
    [InlineData("Dune written by , Date 1965", ", Date 1965")]
    public void ACommaNotFollowedByAQualifier_StaysInTheName(string text, string right)
    {
        var request = Parse(text);

        Assert.Equal(right, request.Right);
        Assert.Empty(request.QualifiersTyped);
    }

    [Fact]
    public void ACommaInAValue_StaysInIt_AndTheLongestNameWins()
    {
        var request = Parse("Dune written by Frank Herbert, Place 12 rue de la Paix, Vendôme, Start date 1965-01-01, Date");

        Assert.Equal(
            [new QualifierInput("Place", "12 rue de la Paix, Vendôme"), new QualifierInput("Start date", "1965-01-01"), new QualifierInput("Date", string.Empty)],
            request.QualifiersTyped);
        Assert.True(request.QualifiersTyped[^1].IsRemoval);
    }

    [Fact]
    public void ALinkTypeWithoutQualifiers_ReadsCommasAsNames() =>
        Assert.Equal("Frank Herbert, Date 1965", Parse("Dune relates to Frank Herbert, Date 1965").Right);

    [Fact]
    public void EachQualifierStartingWithTheText_IsProposed_TheShortestFirst() =>
        Assert.Equal([Source, StartDate], QualifierText.Proposed("Frank Herbert, s", [StartDate, Date, Source]));

    [Fact]
    public void AQualifierName_IsProposedAfterAComma()
    {
        Assert.Equal(Source, QualifierText.Proposed("Frank Herbert, so", [Date, Source]).FirstOrDefault());
        Assert.Empty(QualifierText.Proposed("Frank Herbert, Source", [Date, Source]));
        Assert.Empty(QualifierText.Proposed("Frank Herbert, so ", [Date, Source]));
        Assert.Empty(QualifierText.Proposed(", so", [Date, Source]));
        Assert.Equal(Source, QualifierText.Proposed(", so", [Date, Source], nodeTyped: true).FirstOrDefault());
        Assert.Empty(QualifierText.Proposed("Frank Herbert", [Date, Source]));
    }

    [Fact]
    public void ANewLink_IsAddedWithItsQualifiers()
    {
        var plan = Plan("Dune written by Frank Herbert, Date 1965-08-01, Source isfdb.org, Start date");
        var preview = Preview("Dune written by Frank Herbert, Date 1965-08-01, Source isfdb.org");

        Apply(plan);

        var link = _graph.LinksOf(_dune.Id).Single();
        Assert.Equal(["1965-08-01", "https://isfdb.org", null], NodeFields.OfLink(_graph, link).Take(3).Select(field => field.Value));
        Assert.False(plan.IsUpdate);
        Assert.Equal("Enter to add the link “Dune written by Frank Herbert” (Date 1965-08-01, Source https://isfdb.org).", preview);
    }

    [Fact]
    public void AValueThatDoesNotFit_RefusesThePlan()
    {
        var plan = Plan("Dune written by Frank Herbert, Date soon");

        Assert.Equal(LinkProblem.QualifierInvalid, plan.Problem);
        Assert.False(plan.CanAdd);
        Assert.Equal("Date: Type a date, like 2026-10-27, or a month (2026-10) or a year (2026)", Preview("Dune written by Frank Herbert, Date soon"));
    }

    [Fact]
    public void TheSentenceAgain_UpdatesTheLink_EitherWayItIsRead()
    {
        var link = Link(_frank, _dune, date: "1965-08-01");

        var plan = Plan("Dune written by Frank Herbert, Date 1966-01-01, Source isfdb.org");

        Assert.True(plan.IsUpdate);
        Assert.Equal(link, plan.Existing);
        Assert.Equal("Enter to update “Dune written by Frank Herbert”: Date: 1965-08-01 → 1966-01-01, Source https://isfdb.org. Up/Down: which link.",
            Preview("Dune written by Frank Herbert, Date 1966-01-01, Source isfdb.org"));
        Apply(plan);
        Assert.Equal("1966-01-01", NodeFields.ShownOf(_graph, link)!.Value);
        Assert.Equal(LinkProblem.QualifiersUnchanged, Plan("Frank Herbert wrote Dune, Date 1966-01-01").Problem);
        Assert.Equal("“Frank Herbert” wrote “Dune” already, with these qualifiers.", Preview("Frank Herbert wrote Dune, Date 1966-01-01"));
    }

    [Fact]
    public void AQualifierWithoutValue_IsTakenOut_NamingWhatGoes()
    {
        var link = Link(_frank, _dune, date: "1965-08-01");

        var plan = Plan("Dune written by Frank Herbert, Date");

        Assert.Equal("Date 1965-08-01 taken out", Assert.Single(plan.Qualifiers).ToString());
        Apply(plan);
        Assert.Null(NodeFields.ShownOf(_graph, link));
    }

    [Fact]
    public void AnInvalidValue_ForAnExistingLink_RefusesThePlan()
    {
        Link(_frank, _dune, date: "1965-08-01");

        Assert.Equal(LinkProblem.QualifierInvalid, Plan("Dune written by Frank Herbert, Date soon").Problem);
    }

    [Fact]
    public void ALinkAlreadyThere_WithoutQualifiers_IsStillThere()
    {
        Link(_frank, _dune);

        var plan = Plan("Dune written by Frank Herbert");

        Assert.Equal(LinkProblem.AlreadyLinked, plan.Problem);
        Assert.False(plan.HasLinkChoice);
    }

    [Fact]
    public void WithSeveralLinks_TheChoiceSaysWhichIsUpdated_OrAddsASeparateOne()
    {
        var first = Link(_frank, _dune, date: "1965-08-01");
        var second = Link(_frank, _dune, separate: true, date: "1980-01-01");

        Assert.Equal(first, Plan("Dune written by Frank Herbert, Date 1966-01-01").Existing);
        Assert.Equal(second, Plan("Dune written by Frank Herbert, Date 1966-01-01", new AddChoices(Link: 1)).Existing);
        var added = Plan("Dune written by Frank Herbert, Date 1966-01-01", new AddChoices(Link: 5));
        Assert.Null(added.Existing);
        Assert.Equal(2, added.LinkChoice);
        Assert.True(added.Changes.OfType<LinkAdded>().Single().Link.IsSeparate);
        Assert.Equal("Enter to add another link “Dune written by Frank Herbert” (Date 1966-01-01). Up/Down: which link.", Preview("Dune written by Frank Herbert, Date 1966-01-01", new AddChoices(Link: 2)));
    }

    [Fact]
    public void TheInputBox_ListsTheLinksToChooseFrom_AndUpDownChooses()
    {
        Link(_frank, _dune, date: "1965-08-01");
        var presenter = new InputPresenter();
        presenter.Update(_graph, "Dune written by Frank Herbert, Date 1966-01-01");

        Assert.Equal([("written by Frank Herbert · 1965-08-01", true), ("+ a new link", false)], presenter.Rows.Select(row => (row.Text, row.IsChosen)));
        Assert.True(presenter.MoveChoice(1));
        Assert.Equal(1, presenter.Choices.Link);
        Assert.False(((LinkPlan)presenter.Plan!).IsUpdate);
        Assert.True(presenter.Select(presenter.Rows[0]));
        Assert.Equal(0, presenter.Choices.Link);
        Assert.False(presenter.Select(presenter.Rows[0]));
        presenter.Update(_graph, "Dune written by Frank Herbert");
        Assert.False(presenter.MoveChoice(1));
    }

    [Fact]
    public void TheCreationScreen_ShowsTheLinkTypesQualifiers_ToFill()
    {
        var link = Link(_frank, _dune, date: "1965-08-01");
        var form = new CreationForm(_graph, Parse("Dune written by Frank Herbert, Source isfdb.org"), clock: _clock);

        Assert.Equal("Update a link", form.Title);
        Assert.Equal([("Date", string.Empty, "1965-08-01"), ("Source", "isfdb.org", null), ("Start date", string.Empty, null), ("Place", string.Empty, null)],
            form.Qualifiers.Select(line => (line.Field.Name, line.Text, line.Current)));

        form.SetQualifier(Date, " 1966-01-01 ");
        form.SetQualifier(Source, " ");

        Assert.Equal("Updates “Dune written by Frank Herbert”: Date: 1965-08-01 → 1966-01-01.", form.Status);
        Assert.Equal(link, ((LinkPlan)form.Plan).Existing);
        Assert.Empty(new CreationForm(_graph, new NodeRequest("Arrakis")).Qualifiers);
        Assert.Throws<InvalidOperationException>(() => new CreationForm(_graph, new NodeRequest("Arrakis")).SetQualifier(Date, "1965"));
    }

    [Fact]
    public void TheDetailsLinkBox_ReadsQualifiersToo()
    {
        Link(_frank, _dune, date: "1965-08-01");

        Assert.Equal("Enter to update “Dune written by Frank Herbert”: Date: 1965-08-01 → 1966-01-01.", DetailsLink.Of(_graph, _dune, "written by Frank Herbert, Date 1966-01-01").Preview);
    }

    private string Preview(string text, AddChoices? choices = null)
    {
        var presenter = new InputPresenter();
        presenter.Update(_graph, text);
        for (var step = 0; step < (choices?.Link ?? 0); step++)
        {
            presenter.MoveChoice(1);
        }

        return presenter.Preview;
    }
}
