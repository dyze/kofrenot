namespace Kofrenot.Core.Tests.Views;

/// <summary>Query views: a query read into a definition (<see cref="QueryDraft"/>), and the rows it finds (<see cref="QueryView"/>).</summary>
public sealed class QueryViewTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 9, 0, 0, TimeSpan.Zero);
    private static readonly FieldDefinition Date = new("date", "Date", FieldKind.Date);
    private static readonly FieldDefinition Published = new("published", "Published", FieldKind.Number);
    private static readonly FieldDefinition Cover = new("cover", "Cover", FieldKind.Image);
    private static readonly NodeType Book = new("book", "Book", [Published, Cover]);
    private static readonly NodeType Article = new("article", "Article", [Published]);
    private static readonly LinkColumn WrittenBy = new("wrote", true);

    private readonly KnowledgeGraph _graph = new(new TypeCatalog([TypeCatalog.Generic, Book, Article], [TypeCatalog.Contains, TypeCatalog.RelatesTo]));
    private readonly Node _alice;
    private readonly Node _dune;
    private readonly Node _foundation;
    private readonly Node _herbert;

    public QueryViewTests()
    {
        _graph.Apply(new LinkTypeDefined(Guid.NewGuid(), Now, "read", "read", "read by", [Date], []));
        _graph.Apply(new LinkTypeDefined(Guid.NewGuid(), Now, "wrote", "wrote", "written by", [], []));
        _alice = Add("Alice");
        _dune = Add("Dune", Book);
        _foundation = Add("Foundation", Book);
        var article = Add("An article", Article);
        _herbert = Add("Frank Herbert");
        var brian = Add("Brian Herbert");
        Link("read", _alice, _dune, "2026-03-14");
        Link("read", _alice, _foundation, "2025-07-01");
        Link("read", _alice, article, "2026-02-01");
        Link("wrote", _herbert, _dune);
        Link("wrote", brian, _dune);
    }

    [Fact]
    public void AQuery_ReadsAsADefinition_ItsNodeById()
    {
        var (definition, problem) = QueryDraft.Read("?book: read by Alice, Date 2026", _graph);

        Assert.Null(problem);
        Assert.Equal(new QueryDefinition("book", "read", true, _alice.Id, [new("date", "2026")], []), definition);
        Assert.Equal(definition, QueryDraft.Read("book: read by alice, date 2026", _graph).Definition);
    }

    [Fact]
    public void AQuery_WithNoNode_FindsThroughAnyNode() =>
        Assert.Null(QueryDraft.Read("?read by, Date 2026", _graph).Definition!.Node);

    [Theory]
    [InlineData("?book:", QueryDraft.Hint)]
    [InlineData("?changed: today", QueryDraft.Hint)]
    [InlineData("Dune", QueryDraft.Hint)]
    [InlineData("?read by Alicia", "No node is named “Alicia”.")]
    public void AQuery_ThatIsNoLink_OrNamesNoNode_IsAProblem(string text, string problem) =>
        Assert.Equal((null, problem), QueryDraft.Read(text, _graph));

    [Fact]
    public void ANodeNamedTwice_IsAProblem()
    {
        Add("Alice");

        Assert.Equal("2 nodes are named “Alice”: rename one to use it here.", QueryDraft.Read("?read by Alice", _graph).Problem);
    }

    [Fact]
    public void ItsText_IsWrittenWithTheNamesOfTheDay()
    {
        var definition = QueryDraft.Read("?book: read by Alice, Date 2026, Date", _graph).Definition!;
        _graph.Apply(new NodeRenamed(Guid.NewGuid(), Now, _alice.Id, "Alice Smith", [.. _graph.NamesOf(_alice.Id).Select(name => name.Id)]));

        Assert.Equal("?Book: read by Alice Smith, Date 2026, Date", QueryDraft.TextOf(definition, _graph));
        Assert.Equal("?read", QueryDraft.TextOf(new QueryDefinition(null, "read", false, Guid.NewGuid(), [], []), _graph));
        Assert.Equal("?shelf: lent, due 2026", QueryDraft.TextOf(new QueryDefinition("shelf", "lent", true, null, [new("due", "2026")], []), _graph));
    }

    [Fact]
    public void TheDraft_KeepsItsColumns_AsItsTextChanges()
    {
        var draft = QueryDraft.Default.WithText("?read by Alice", _graph).WithColumn(WrittenBy, true).WithColumn(new LinkColumn("contains", false), true).WithColumn(new LinkColumn("contains", false), false);

        Assert.Equal([WrittenBy], draft.Definition!.Columns);
        Assert.Equal([WrittenBy], draft.WithText("?book: read by Alice", _graph).Definition!.Columns);
        Assert.Null(draft.WithText("?read by", _graph).WithText("?nothing", _graph).Definition);
        Assert.Equal(draft.Definition, QueryDraft.Of(draft.Definition!, _graph).Definition);
        Assert.Contains(WrittenBy, draft.ColumnChoices(_graph).Select(choice => choice.Column));
    }

    [Fact]
    public void AColumnsName_IsItsLinkPhrase()
    {
        Assert.Equal("written by", QueryDraft.NameOf(WrittenBy, "read", _graph));
        Assert.Equal("relates to", QueryDraft.NameOf(new LinkColumn("relates-to", true), "read", _graph));
        Assert.Equal("lent", QueryDraft.NameOf(new LinkColumn("lent", false), "read", _graph));
    }

    [Fact]
    public void TheRows_AreTheNodesFound_ByName_WithTheirColumns()
    {
        var view = new QueryView(_graph, Definition("?book: read by Alice", WrittenBy, new LinkColumn("contains", false)));

        Assert.Equal(["written by", "contains"], view.Headers);
        Assert.Equal(
            [("Dune", "Brian Herbert, Frank Herbert", ""), ("Foundation", "", "")],
            view.Rows.Select(row => (row.Node.Name, row.Cells[0], row.Cells[1])));
        Assert.Equal([_foundation], view.Find("found").Select(row => row.Node));
        Assert.Equal(["written by Brian Herbert, Frank Herbert", ""], view.Rows.Select(view.LineOf));
    }

    [Fact]
    public void TheQualifiers_AndAnyNode_NarrowTheRows()
    {
        Assert.Equal(["An article", "Dune"], Names("?read by Alice, Date 2026"));
        Assert.Equal(["Dune", "Foundation"], Names("?book: read by"));
        Assert.Equal(["Brian Herbert", "Frank Herbert"], Names("?wrote Dune"));
    }

    [Fact]
    public void ANodeNotHere_FindsNothing()
    {
        var view = new QueryView(_graph, new QueryDefinition(null, "read", true, Guid.NewGuid(), [], []));

        Assert.True(view.IsBroken);
        Assert.Empty(view.Rows);
    }

    [Fact]
    public void AViewDraft_OfAQuery_GivesItBack_AndDescribesIt()
    {
        var definition = Definition("?book: read by Alice, Date 2026", WrittenBy);

        var draft = ViewDraft.Of(definition, _graph);

        Assert.Equal((ViewDisplay.Query, definition), (draft.Display, draft.Definition));
        Assert.Null((ViewDraft.Default with { Display = ViewDisplay.Query }).Definition);
        Assert.Equal("The nodes found by “?Book: read by Alice, Date 2026”, with “written by”", ViewDraft.Describe(definition, _graph));
        Assert.Equal("The nodes found by “?read by Alice”", ViewDraft.Describe(Definition("?read by Alice"), _graph));
        Assert.Equal("A query naming a node not here: removed, or not synced yet", ViewDraft.Describe(new QueryDefinition(null, "read", true, Guid.NewGuid(), [], []), _graph));
    }

    [Fact]
    public void TheStatus_IsTheProblem_OrHowManyNodesAreFound()
    {
        Assert.Equal(QueryDraft.Hint, QueryDraft.Default.StatusIn(_graph));
        Assert.Equal("No node is named “Bob”.", QueryDraft.Default.WithText("?read by Bob", _graph).StatusIn(_graph));
        Assert.Equal("No node found.", QueryDraft.Default.WithText("?read by Frank Herbert", _graph).StatusIn(_graph));
        Assert.Equal("1 node found.", QueryDraft.Default.WithText("?book: read by Alice, Date 2026", _graph).StatusIn(_graph));
        Assert.Equal("3 nodes found.", QueryDraft.Default.WithText("?read by Alice", _graph).StatusIn(_graph));
    }

    [Fact]
    public void AQueryTyped_StartsTheCreationOfItsView()
    {
        var form = CreationForm.QueryView(_graph, " ?read by Alice ");

        Assert.True(form.IsView(Side.Left));
        Assert.Equal(("", Definition("?read by Alice")), (form.Name, form.ViewOf(Side.Left).Definition));
        Assert.Same(_graph, form.Graph);
    }

    [Fact]
    public void TheInputBox_OffersToSaveAQuery_ThatAViewCanShow()
    {
        var input = new InputPresenter();

        input.Update(_graph, "?book: read by Alice, Date 2026");
        Assert.True(input.CanSaveAsView);
        input.Update(_graph, "?book:");
        Assert.False(input.CanSaveAsView);
        input.Update(_graph, "?read by Alicia");
        Assert.False(input.CanSaveAsView);
        input.Update(_graph, "Dune read by Alice");
        Assert.False(input.CanSaveAsView);
    }

    [Fact]
    public void AQueryView_HasTheListIcon()
    {
        var view = Add("Read in 2026", TypeCatalog.View);
        _graph.Apply(new ViewEdited(Guid.NewGuid(), Now, view.Id, Definition("?read by Alice"), []));

        Assert.Equal(NodeIcon.List, NodeVisual.Of(_graph, view).Icon);
    }

    [Fact]
    public void AFieldColumn_ShowsTheValueOfTheNodeFound()
    {
        SetField(_dune, Book, Published, "1965");

        var view = new QueryView(_graph, Definition("?book: read by Alice", new FieldColumn("book", "published")));

        Assert.Equal(["Published"], view.Headers);
        Assert.Equal([("Dune", "1965"), ("Foundation", "")], view.Rows.Select(row => (row.Node.Name, row.Cells[0])));
    }

    [Fact]
    public void AQualifierColumn_ShowsTheValuesOfTheLinksFound_InOrder()
    {
        Link("read", Add("Bob"), _dune, "2024-05-05");
        Link("read", Add("Carol"), _dune);
        var date = new QualifierColumn("date");

        var alices = new QueryView(_graph, Definition("?book: read by Alice", date));
        var anyones = new QueryView(_graph, Definition("?book: read by", date));

        Assert.Equal(["Date"], alices.Headers);
        Assert.Equal([("Dune", "2026-03-14"), ("Foundation", "2025-07-01")], alices.Rows.Select(row => (row.Node.Name, row.Cells[0])));
        Assert.Equal("Date 2024-05-05, 2026-03-14", anyones.LineOf(anyones.Rows[0]));
        Assert.Equal(["2026-03-14"], new QueryView(_graph, Definition("?book: read by, Date 2026", date)).Rows.Select(row => row.Cells[0]));
    }

    [Fact]
    public void TheChoices_AreTheFieldsOfItsType_AndTheQualifiersOfItsLink()
    {
        var draft = QueryDraft.Default.WithText("?book: read by Alice", _graph);

        var choices = draft.ColumnChoices(_graph);

        Assert.Contains(new ColumnChoice(WrittenBy, "written by"), choices);
        Assert.Equal([new ColumnChoice(new FieldColumn("book", "published"), "Published")], choices.Where(choice => choice.Column is FieldColumn));
        Assert.Equal([new ColumnChoice(new QualifierColumn("date"), "Date")], choices.Where(choice => choice.Column is QualifierColumn));
        Assert.Equal("Qualifiers of “read by”", draft.QualifiersHeading(_graph));
    }

    [Fact]
    public void WithNoType_TheFieldsAreThoseOfTheNodesFound_NamedByTypeWhenAlike()
    {
        var choices = QueryDraft.Default.WithText("?read by Alice", _graph).ColumnChoices(_graph);

        Assert.Equal(["Published (Article)", "Published (Book)"], choices.Where(choice => choice.Column is FieldColumn).Select(choice => choice.Name));
        Assert.DoesNotContain(QueryDraft.Default.ColumnChoices(_graph), choice => choice.Column is not LinkColumn);
        Assert.Equal("Qualifiers", QueryDraft.Default.QualifiersHeading(_graph));
    }

    [Fact]
    public void AColumnShown_ButNoLongerOffered_StaysToBeTakenOff()
    {
        var gone = new FieldColumn("book", "gone");

        var choices = QueryDraft.Default.WithText("?book: read by Alice", _graph).WithColumn(gone, true).ColumnChoices(_graph);

        Assert.Equal(new ColumnChoice(gone, "gone"), choices[^1]);
    }

    [Fact]
    public void AFieldOrQualifierColumnsName_IsItsFields()
    {
        Assert.Equal("Published", QueryDraft.NameOf(new FieldColumn("article", "published"), null, _graph));
        Assert.Equal("Date", QueryDraft.NameOf(new QualifierColumn("date"), "read", _graph));
        Assert.Equal("date", QueryDraft.NameOf(new QualifierColumn("date"), "wrote", _graph));
        Assert.Equal("date", QueryDraft.NameOf(new QualifierColumn("date"), null, _graph));
    }

    [Fact]
    public void Numbers_SortAsNumbers_EmptyCellsLast_EitherWay()
    {
        SetField(_dune, Book, Published, "1965");
        SetField(_foundation, Book, Published, "200");
        var view = new QueryView(_graph, Definition("?read by Alice", new FieldColumn("book", "published")));

        Assert.Equal(["Foundation", "Dune", "An article"], SortedNames(view, new QuerySort(1, false)));
        Assert.Equal(["Dune", "Foundation", "An article"], SortedNames(view, new QuerySort(1, true)));
    }

    [Fact]
    public void Dates_SortInOrder()
    {
        var view = new QueryView(_graph, Definition("?read by Alice", new QualifierColumn("date")));

        Assert.Equal(["Foundation", "An article", "Dune"], SortedNames(view, new QuerySort(1, false)));
    }

    [Fact]
    public void Ties_StayByName_AndTheNameColumnSortsBothWays()
    {
        SetField(_dune, Book, Published, "1965");
        SetField(_foundation, Book, Published, "1965");
        var view = new QueryView(_graph, Definition("?book: read by Alice", new FieldColumn("book", "published")));

        Assert.Equal(["Dune", "Foundation"], SortedNames(view, new QuerySort(1, false)));
        Assert.Equal(["Dune", "Foundation"], SortedNames(view, new QuerySort(1, true)));
        Assert.Equal(["Foundation", "Dune"], SortedNames(view, new QuerySort(QuerySort.NameColumn, true)));
    }

    [Fact]
    public void Found_InTheOrderShown()
    {
        Link("read", _alice, Add("Dune Messiah", Book));
        var view = new QueryView(_graph, Definition("?book: read by Alice"));

        Assert.Equal(["Dune Messiah", "Dune"], view.Find("dune", new QuerySort(QuerySort.NameColumn, true)).Select(row => row.Node.Name));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(-1)]
    public void AColumnNotHere_SortsByName(int column) =>
        Assert.Equal(["Dune", "Foundation"], SortedNames(new QueryView(_graph, Definition("?book: read by Alice", WrittenBy)), new QuerySort(column, true)));

    [Theory]
    [InlineData("200", "1965", -1)]
    [InlineData("Éclair", "eclat", -1)]
    [InlineData("abc", "ABC", 0)]
    [InlineData("2025-07-01", "2026", -1)]
    public void Cells_CompareAsWhatTheyAre(string first, string second, int sign) =>
        Assert.Equal(sign, Math.Sign(QuerySort.CompareCells(first, second)));

    [Fact]
    public void AClickOnAHeader_SortsByIt_ThenTheOtherWay()
    {
        Assert.Equal(new QuerySort(0, true), QuerySort.ByName.ClickedOn(0));
        Assert.Equal(new QuerySort(0, false), new QuerySort(0, true).ClickedOn(0));
        Assert.Equal(new QuerySort(2, false), new QuerySort(1, true).ClickedOn(2));
    }

    private static List<string> SortedNames(QueryView view, QuerySort sort) => [.. view.RowsBy(sort).Select(row => row.Node.Name)];

    private void SetField(Node node, NodeType type, FieldDefinition field, string value) =>
        _graph.Apply(new FieldSet(Guid.NewGuid(), Now, node, type.Id, field.Id, field.Name, field.Kind, value, []));

    private List<string> Names(string query) => [.. new QueryView(_graph, Definition(query)).Rows.Select(row => row.Node.Name)];

    private QueryDefinition Definition(string query, params ViewColumn[] columns) =>
        QueryDraft.Read(query, _graph).Definition! with { Columns = columns };

    private Node Add(string name, NodeType? type = null)
    {
        var node = new Node(Guid.NewGuid(), (type ?? TypeCatalog.Generic).Id, name, Now);
        _graph.Add(node);
        return node;
    }

    private void Link(string type, Node from, Node to, string? date = null)
    {
        var link = new Link(Guid.NewGuid(), type, from.Id, to.Id, Now);
        _graph.Apply(new LinkAdded(Guid.NewGuid(), Now, link));
        if (date is not null)
        {
            _graph.Apply(new FieldSet(Guid.NewGuid(), Now, link, type, Date.Id, Date.Name, FieldKind.Date, date, []));
        }
    }
}
