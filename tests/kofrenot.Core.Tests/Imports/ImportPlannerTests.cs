namespace Kofrenot.Core.Tests.Imports;

public sealed class ImportPlannerTests
{
    private readonly BookVault _vault = new();

    [Fact]
    public void AFirstImport_AddsEachBook_WithItsValues_AndItsAuthorsOnce()
    {
        var plan = _vault.Plan("""
            [
              { "isbn": "978-0441013593", "title_fr": "Dune", "title": "Dune", "year": 1965, "publisher": { "name": "Ace" }, "authors": ["Frank Herbert"] },
              { "title_fr": "De bons présages", "title": "Good Omens", "authors": ["Terry Pratchett", "Neil Gaiman", "terry pratchett"] },
              { "title": "Strata", "authors": ["Terry Pratchett"] }
            ]
            """);

        var changes = _vault.Import(plan);

        Assert.Equal([RecordOutcome.New, RecordOutcome.New, RecordOutcome.New], plan.Items.Select(item => item.Outcome));
        Assert.Equal(["Original title: Dune", "ISBN: 978-0441013593", "Published: 1965", "Publisher: Ace", "written by: + Frank Herbert (new)"], plan.Items[0].Details);
        Assert.Equal(3, plan.AddedFor(_ => RecordChoice.Import));
        Assert.Equal(3, plan.CountFor(_ => RecordChoice.Import));
        var dune = Assert.Single(_vault.Graph.NodesNamed("Dune"));
        Assert.Equal(("book", "978-0441013593", "1965", "Ace"), (dune.Type, _vault.ValueOf(dune, BookVault.Isbn), _vault.ValueOf(dune, BookVault.Published), _vault.ValueOf(dune, BookVault.Publisher)));
        var pratchett = Assert.Single(_vault.Graph.NodesNamed("Terry Pratchett"));
        Assert.Equal("person", pratchett.Type);
        Assert.Equal(2, _vault.Graph.LinksOf(pratchett.Id).Count(link => link.From == pratchett.Id && link.Type == "wrote"));
        Assert.Equal(6, changes.OfType<NodeAdded>().Count());
    }

    [Fact]
    public void ARecordWithoutName_IsNotImported()
    {
        var item = _vault.Plan("[{ \"isbn\": \"978-0000000000\" }]").Items[0];

        Assert.Equal((RecordOutcome.NotImported, "no “title_fr” nor “title”"), (item.Outcome, item.Reason));
    }

    [Fact]
    public void AnImportRunAgain_FindsItsNodes_AndChangesNothing()
    {
        const string Json = "[{ \"isbn\": \"978-0441013593\", \"title_fr\": \"Dune\", \"year\": 1965, \"authors\": [\"Frank Herbert\"] }]";
        _vault.Import(_vault.Plan(Json));

        var again = _vault.Plan(Json);

        Assert.Equal(RecordOutcome.Unchanged, again.Items[0].Outcome);
        Assert.Empty(again.ChangesFor(_ => RecordChoice.Import, BookVault.Start));
        Assert.Equal(0, again.CountFor(_ => RecordChoice.Import));
    }

    [Fact]
    public void ABookFoundByItsIsbn_IsUpdated_OnlyWhatDiffers_NeverCleared()
    {
        var dune = _vault.Add("Dune");
        _vault.Set(dune, BookVault.Isbn, "978-0441013593");
        _vault.Set(dune, BookVault.Published, "1966");
        _vault.Set(dune, BookVault.Publisher, "Chilton");
        var herbert = _vault.Add("Frank Herbert", "person");
        _vault.Link(herbert, dune);

        var plan = _vault.Plan("[{ \"isbn\": \"978-0441013593\", \"title_fr\": \"Dune (Deluxe)\", \"year\": 1965, \"authors\": [\"Frank Herbert\", \"Kevin J. Anderson\"] }]");
        var item = plan.Items[0];
        var changes = _vault.Import(plan);

        Assert.Equal((RecordOutcome.Updated, dune), (item.Outcome, item.Node));
        Assert.Equal(["name: Dune → Dune (Deluxe)", "Published: 1966 → 1965", "written by: + Kevin J. Anderson (new)"], item.Details);
        Assert.Equal("Dune (Deluxe)", _vault.Graph.FindNode(dune.Id)!.Name);
        Assert.Equal(("1965", "Chilton"), (_vault.ValueOf(dune, BookVault.Published), _vault.ValueOf(dune, BookVault.Publisher)));
        Assert.Single(changes.OfType<LinkAdded>());
        Assert.Single(_vault.Graph.FieldsOf(dune.Id), field => field.Field == BookVault.Published.Id);
    }

    [Fact]
    public void AValueInConflict_IsWrittenOverBothValues()
    {
        var dune = _vault.Add("Dune");
        var first = _vault.Set(dune, BookVault.Published, "1965");
        var second = _vault.Set(dune, BookVault.Published, "1966");

        var update = Assert.IsType<FieldSet>(Assert.Single(_vault.Plan("[{ \"title_fr\": \"Dune\", \"year\": 1965 }]").ChangesFor(_ => RecordChoice.Import, BookVault.Start)));

        Assert.Equal(new HashSet<Guid>([first.Id, second.Id]), update.Replaces.ToHashSet());
    }

    [Fact]
    public void WithoutIsbn_ABookIsFoundByItsName_AndGetsTheIsbn()
    {
        var dune = _vault.Add("dune");

        var item = _vault.Plan("[{ \"isbn\": \"978-0441013593\", \"title_fr\": \"Dune\" }]").Items[0];

        Assert.Equal((RecordOutcome.Updated, dune), (item.Outcome, item.Node));
        Assert.Equal(["name: dune → Dune", "ISBN: (empty) → 978-0441013593"], item.Details);
    }

    [Fact]
    public void ABookFirstImportedInEnglish_IsFoundByItsOriginalTitle_AndRenamedInFrench()
    {
        var dispossessed = _vault.Add("The Dispossessed");
        _vault.Set(dispossessed, BookVault.OriginalTitle, "The Dispossessed");
        var other = _vault.Add("Neuromancer");

        var plan = _vault.Plan("""
            [
              { "title_fr": "Les Dépossédés", "title": "The Dispossessed" },
              { "title_fr": "Neuromancien", "title": "Neuromancer" }
            ]
            """);

        Assert.Equal(["found by Original title “The Dispossessed”", "name: The Dispossessed → Les Dépossédés"], plan.Items[0].Details);
        Assert.Equal(["found by Original title “Neuromancer”", "name: Neuromancer → Neuromancien", "Original title: (empty) → Neuromancer"], plan.Items[1].Details);
        Assert.Equal(other, plan.Items[1].Node);
    }

    [Fact]
    public void ASameNameWithAnotherIsbn_IsToCheck_UpdatedOrAddedAsNewOrSkipped()
    {
        var dune = _vault.Add("Dune");
        _vault.Set(dune, BookVault.Isbn, "978-0441172719");
        var plan = _vault.Plan("[{ \"isbn\": \"978-0441013593\", \"title_fr\": \"Dune\" }]");
        var item = plan.Items[0];

        Assert.Equal(RecordOutcome.ToCheck, item.Outcome);
        Assert.Equal(["ISBN: 978-0441172719 → 978-0441013593"], item.Details);
        Assert.Empty(plan.ChangesFor(_ => RecordChoice.Skip, BookVault.Start));
        Assert.Equal(0, plan.CountFor(_ => RecordChoice.Skip));
        Assert.IsType<NodeAdded>(plan.ChangesFor(_ => RecordChoice.AddAsNew, BookVault.Start)[0]);
        Assert.Equal((1, 1, 0), (plan.AddedFor(_ => RecordChoice.AddAsNew), plan.CountFor(_ => RecordChoice.Import), plan.AddedFor(_ => RecordChoice.Import)));

        _vault.Import(plan);

        Assert.Equal("978-0441013593", _vault.ValueOf(dune, BookVault.Isbn));
    }

    [Fact]
    public void SeveralNodes_ForOneRecord_AreAmbiguous()
    {
        var first = _vault.Add("Solaris");
        _vault.Add("Solaris");
        _vault.Set(first, BookVault.Isbn, "1");
        _vault.Set(_vault.Add("Other"), BookVault.Isbn, "1");

        var plan = _vault.Plan("[{ \"title\": \"Solaris\" }, { \"title\": \"Dune\", \"isbn\": \"1\" }]");

        Assert.Equal((RecordOutcome.NotImported, "2 nodes have this name"), (plan.Items[0].Outcome, plan.Items[0].Reason));
        Assert.Equal("2 nodes have this ISBN", plan.Items[1].Reason);
    }

    [Fact]
    public void TwoRecords_ForOneNode_AreNeitherImported()
    {
        _vault.Add("Dune");

        var plan = _vault.Plan("""
            [
              { "title": "Dune" }, { "title": "dune", "year": 1965 },
              { "title": "Solaris", "isbn": "1" }, { "title": "Solaris (Folio)", "isbn": "1" },
              { "title": "Strata" }
            ]
            """);

        Assert.Equal(
            ["same node as record 2", "same node as record 1", "same node as record 4", "same node as record 3", null],
            plan.Items.Select(item => item.Reason));
        Assert.Equal(RecordOutcome.New, plan.Items[4].Outcome);
    }

    [Fact]
    public void AValueThatDoesNotFit_OrExtraValues_AreListed_TheRestImported()
    {
        var item = _vault.Plan("[{ \"title\": \"Neuromancer\", \"year\": \"c. 1984\", \"isbn\": [\"1\", \"2\", \"3\"] }]").Items[0];

        Assert.Equal(RecordOutcome.New, item.Outcome);
        Assert.Equal(["ISBN: 1"], item.Details.Where(detail => !detail.StartsWith("Original", StringComparison.Ordinal)));
        Assert.Equal(2, item.ValueProblems.Count);
        Assert.Equal("isbn: 2 more value(s), ISBN holds one", item.ValueProblems[0]);
        Assert.StartsWith("year “c. 1984”: ", item.ValueProblems[1]);
    }

    [Fact]
    public void AnAuthorNamedTwice_IsListed_NotLinked()
    {
        _vault.Add("Frank Herbert", "person");
        _vault.Add("Frank Herbert", "person");
        _vault.Add("Frank Herbert");

        var item = _vault.Plan("[{ \"title\": \"Dune\", \"authors\": [\"Frank Herbert\"] }]").Items[0];

        Assert.Equal(["Original title: Dune"], item.Details);
        Assert.Equal("authors “Frank Herbert”: 2 nodes have this name", Assert.Single(item.ValueProblems));
    }

    [Fact]
    public void AnUncheckedRecord_IsNotWritten_NorTheAuthorsOnlyItNeeds()
    {
        var plan = _vault.Plan("[{ \"title\": \"Dune\", \"authors\": [\"Frank Herbert\"] }, { \"title\": \"Strata\" }]");

        _vault.Import(plan, item => item.Name == "Dune" ? RecordChoice.Skip : RecordChoice.Import);

        Assert.Empty(_vault.Graph.NodesNamed("Frank Herbert"));
        Assert.Single(_vault.Graph.NodesNamed("Strata"));
    }

    [Fact]
    public void AMappingWithoutMatch_FindsByName_AndOfAGoneTypeIsRefused()
    {
        var dune = _vault.Add("Dune");
        _vault.Set(dune, BookVault.Isbn, "1");

        var item = _vault.Plan("[{ \"title\": \"Dune\", \"isbn\": \"2\" }]", BookVault.Books with { Match = null }).Items[0];
        var refused = _vault.Plan("[]", BookVault.Books with { NodeType = "film" });

        Assert.Equal(RecordOutcome.Updated, item.Outcome);
        Assert.NotNull(refused.Refusal);
        Assert.Empty(refused.Items);
    }

    [Fact]
    public void RulesWhoseFieldOrTypeIsGone_AreLeftOut_AndNamed()
    {
        var mapping = BookVault.Books with
        {
            Match = "gone",
            Fields = [new FieldRule("isbn", BookVault.Isbn.Id), new FieldRule("cover", BookVault.Cover.Id), new FieldRule("pages", "gone")],
            Links = [new LinkRule("authors", "person", "drew", IsReverse: false), new LinkRule("series", "series", "wrote", IsReverse: false)],
        };

        var plan = _vault.Plan("[{ \"title\": \"Dune\", \"cover\": \"dune.jpg\", \"authors\": [\"X\"] }]", mapping);

        Assert.Equal(5, plan.Warnings.Count);
        Assert.Contains(plan.Warnings, warning => warning.Contains("by their names only", StringComparison.Ordinal));
        Assert.Equal(RecordOutcome.New, plan.Items[0].Outcome);
        Assert.Empty(plan.Items[0].Details);
    }

    [Fact]
    public void AForwardLink_GoesFromTheImportedNode_AndReadsForward()
    {
        var herbert = _vault.Add("Frank Herbert", "person");
        var mapping = BookVault.Books with { Links = [new LinkRule("author", "person", "wrote", IsReverse: false)] };

        var plan = _vault.Plan("[{ \"title\": \"Dune\", \"author\": \"Frank Herbert\" }]", mapping);
        var link = Assert.Single(_vault.Import(plan).OfType<LinkAdded>()).Link;

        Assert.Equal("wrote: + Frank Herbert", plan.Items[0].Details[^1]);
        Assert.Equal(herbert.Id, link.To);
        Assert.Equal(RecordOutcome.Unchanged, _vault.Plan("[{ \"title\": \"Dune\", \"author\": \"Frank Herbert\" }]", mapping).Items[0].Outcome);
    }

    [Fact]
    public void ThePlan_GroupsItsRecordsByOutcome()
    {
        _vault.Add("Dune");

        var plan = _vault.Plan("[{ \"title\": \"Dune\", \"year\": 1965 }, { \"title\": \"Strata\" }, {}]");

        Assert.Equal(["Strata"], plan.Of(RecordOutcome.New).Select(item => item.Name));
        Assert.Single(plan.Of(RecordOutcome.Updated));
        Assert.Single(plan.Of(RecordOutcome.NotImported));
        Assert.False(plan.Of(RecordOutcome.NotImported)[0].IsChosen);
    }
}
