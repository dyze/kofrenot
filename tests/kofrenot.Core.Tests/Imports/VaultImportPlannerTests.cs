namespace Kofrenot.Core.Tests.Imports;

/// <summary>Nodes of another vault imported into the open one: found, added, updated, to check or not imported.</summary>
public sealed class VaultImportPlannerTests
{
    private readonly TwoVaults _vaults = new();

    private KnowledgeGraph Source => _vaults.Source;

    private KnowledgeGraph Target => _vaults.Target;

    [Fact]
    public void ANewNode_IsAdded_WithItsId_Types_Colour_Fields_AndNote_ItsTypeCopied()
    {
        _vaults.DefineBook(Source);
        var dune = TwoVaults.Add(Source, "Dune", "book");
        Source.Apply(new NodeTypeAdded(Guid.NewGuid(), TwoVaults.Start.AddMinutes(1), dune.Id, "note"));
        Source.Apply(new NodeColored(Guid.NewGuid(), TwoVaults.Start, dune.Id, "green", []));
        TwoVaults.Set(Source, dune, TwoVaults.Published, "1965");
        var note = _vaults.Note(Source, dune, "# Dune");

        var plan = _vaults.Plan([dune]);
        var write = _vaults.Write(plan);

        var item = Assert.Single(plan.Of(RecordOutcome.New));
        Assert.Equal(["Book, Note", "Published: 1965", "note"], item.Details);
        Assert.Equal(["Book"], plan.NodeTypesAdded);
        Assert.Equal(["Also adds node type Book"], plan.Summary);
        Assert.Equal((1, 1), (plan.CountFor(_ => RecordChoice.Import), plan.AddedFor(_ => RecordChoice.Import)));
        var added = Target.FindNode(dune.Id)!;
        Assert.Equal(("Dune", _vaults.Clock.Now), (added.Name, added.Created));
        Assert.Equal(["book", "note"], Target.TypeIdsOf(dune.Id));
        Assert.Equal("Book", Target.Types.NodeTypeName("book"));
        Assert.Equal(NodeColor.Green, Target.ColorOf(dune.Id));
        Assert.Equal("1965", Target.FieldsOf(dune.Id).Single().Shown.Value);
        var copy = Assert.Single(write.Contents);
        Assert.Equal(note.Id, copy.From.Id);
        Assert.Equal("# Dune", _vaults.Notes[Assert.Single(Target.NoteVersionsOf(dune.Id)).Id]);
    }

    [Fact]
    public void AnImportedNode_ImportedAgain_IsUnchanged_ThenUpdated_WithWhatDiffers()
    {
        _vaults.DefineBook(Source);
        var dune = TwoVaults.Add(Source, "Dune", "book");
        var published = TwoVaults.Set(Source, dune, TwoVaults.Published, "1965");
        _vaults.Note(Source, dune, "# Dune");
        _vaults.File(Source, dune, "dune.pdf", "aaa");
        _vaults.Write(_vaults.Plan([dune]));
        Assert.Single(_vaults.Plan([dune]).Of(RecordOutcome.Unchanged));

        Source.Apply(new NodeRenamed(Guid.NewGuid(), TwoVaults.Start, dune.Id, "Dune (1965)", [dune.Id]));
        Source.Apply(new NodeTypeAdded(Guid.NewGuid(), TwoVaults.Start, dune.Id, "dataset"));
        Source.Apply(new NodeColored(Guid.NewGuid(), TwoVaults.Start, dune.Id, "blue", []));
        Source.Apply(published with { Id = Guid.NewGuid(), Value = "1966", Replaces = [published.Id] });
        _vaults.Note(Source, dune, "# Dune, again");
        var plan = _vaults.Plan([dune]);
        _vaults.Write(plan);

        var item = Assert.Single(plan.Of(RecordOutcome.Updated));
        Assert.Equal(["name: Dune → Dune (1965)", "+ type Dataset", "colour: none → blue", "Published: 1965 → 1966", "note changed"], item.Details);
        Assert.Equal(0, plan.AddedFor(_ => RecordChoice.Import));
        Assert.Equal("Dune (1965)", Target.FindNode(dune.Id)!.Name);
        Assert.True(Target.HasType(dune.Id, "dataset"));
        Assert.Equal(NodeColor.Blue, Target.ColorOf(dune.Id));
        Assert.Equal("1966", Assert.Single(Assert.Single(Target.FieldsOf(dune.Id)).Current).Value);
        Assert.Equal("# Dune, again", _vaults.Notes[Assert.Single(Target.NoteVersionsOf(dune.Id)).Id]);
        Assert.Single(_vaults.Plan([dune]).Of(RecordOutcome.Unchanged));
    }

    [Fact]
    public void AFile_IsCopied_ThenANewVersionOfIt_ReplacesTheOneHere()
    {
        var survey = TwoVaults.Add(Source, "Survey", "dataset");
        _vaults.File(Source, survey, "survey.csv", "aaa");
        Assert.Contains("file: survey.csv", _vaults.Plan([survey]).Items[0].Details);
        _vaults.Write(_vaults.Plan([survey]));

        _vaults.File(Source, survey, "survey-2.csv", "bbb");
        var plan = _vaults.Plan([survey]);
        _vaults.Write(plan);

        Assert.Equal(["file: survey.csv → survey-2.csv"], plan.Items[0].Details);
        Assert.Equal("bbb", Assert.Single(Target.DatasetVersionsOf(survey.Id)).File.Sha256);
    }

    [Fact]
    public void ContentMissingHere_IsAdded_ANoteNotDownloadedHere_Replaced()
    {
        var (survey, ideas, plans) = (TwoVaults.Add(Source, "Survey"), TwoVaults.Add(Source, "Ideas"), TwoVaults.Add(Source, "Plans"));
        TwoVaults.Add(Target, "Survey", id: survey.Id);
        TwoVaults.Add(Target, "Ideas", id: ideas.Id);
        var here = TwoVaults.Add(Target, "Plans", id: plans.Id);
        _vaults.File(Source, survey, "survey.csv", "aaa");
        _vaults.Note(Source, ideas, "# Ideas");
        _vaults.Note(Source, plans, "# Plans");
        _vaults.Note(Target, here, text: null);

        var details = _vaults.Plan([survey, ideas, plans]).Items.Select(item => item.Details.Single()).ToList();

        Assert.Equal(["+ file: survey.csv", "+ note", "note changed"], details);
    }

    [Fact]
    public void ImagesAreCopied_ByTheirFile_AndTheSameFile_IsUnchanged()
    {
        _vaults.DefineBook(Source);
        var dune = TwoVaults.Add(Source, "Dune", "book");
        var cover = TwoVaults.Set(Source, dune, TwoVaults.Cover, null, new DatasetFile("cover.png", 10, "aaa"));
        _vaults.Files.Add(cover.Id);

        var write = _vaults.Write(_vaults.Plan([dune]));

        Assert.Equal(cover.Id, Assert.Single(write.Contents).From.Id);
        Assert.Equal("cover.png", Target.FieldsOf(dune.Id).Single().Shown.File!.Name);
        Assert.Single(_vaults.Plan([dune]).Of(RecordOutcome.Unchanged));
    }

    [Fact]
    public void ContentNotThere_OfAFolderVault_IsNotImported()
    {
        var (dune, survey, ideas) = NodesWithContentNotThere();

        var plan = _vaults.Plan([dune, survey, ideas]);

        Assert.Equal(
            ["its image “Cover” is not in “Work” on this computer: sync it, then import again", "its file is not in “Work” on this computer: sync it, then import again", "its note is not in “Work” on this computer: sync it, then import again"],
            plan.Items.Select(item => item.Reason));
        Assert.Empty(plan.NodeTypesAdded);
        Assert.Empty(plan.WriteFor(_ => RecordChoice.Import).Changes);
    }

    [Fact]
    public void ContentNotThere_OfAServerVault_IsDownloadedOnImport()
    {
        _vaults.CanDownload = true;
        var (dune, survey, ideas) = NodesWithContentNotThere();
        var here = TwoVaults.Add(Target, "Ideas", id: ideas.Id);
        _vaults.Note(Target, here, "# Ideas");

        var plan = _vaults.Plan([dune, survey, ideas]);
        var write = plan.WriteFor(_ => RecordChoice.Import);

        Assert.Equal(["Book", "Cover: cover.png", "downloaded on import: image “Cover”"], plan.Items[0].Details);
        Assert.Equal(["file: survey.csv", "downloaded on import: file"], plan.Items[1].Details);
        Assert.Equal(["note changed", "downloaded on import: note"], plan.Items[2].Details);
        Assert.Equal(3, write.Contents.Count);
    }

    private (Node Dune, Node Survey, Node Ideas) NodesWithContentNotThere()
    {
        _vaults.DefineBook(Source);
        var (dune, survey, ideas) = (TwoVaults.Add(Source, "Dune", "book"), TwoVaults.Add(Source, "Survey"), TwoVaults.Add(Source, "Ideas"));
        TwoVaults.Set(Source, dune, TwoVaults.Cover, null, new DatasetFile("cover.png", 10, "aaa"));
        _vaults.File(Source, survey, "survey.csv", "aaa", isHere: false);
        _vaults.Note(Source, ideas, text: null);
        return (dune, survey, ideas);
    }

    [Fact]
    public void ANodeOfTheSameName_IsToCheck_UpdatedByDefault_AddedAsNew_OrSkipped()
    {
        var dune = TwoVaults.Add(Source, "Dune");
        TwoVaults.Set(Source, dune, TwoVaults.Published, "1965", owner: FieldSet.Own);
        var here = TwoVaults.Add(Target, "dune");

        var plan = _vaults.Plan([dune]);

        var item = Assert.Single(plan.Of(RecordOutcome.ToCheck));
        Assert.Equal(here, item.Found);
        Assert.Equal(["same name as a node here", "name: dune → Dune", "Published: (empty) → 1965"], item.Details);
        Assert.Empty(plan.WriteFor(_ => RecordChoice.Skip).Changes);
        Assert.Equal(1, plan.AddedFor(_ => RecordChoice.AddAsNew));
        Assert.Equal(dune.Id, plan.WriteFor(_ => RecordChoice.AddAsNew).Changes.OfType<NodeAdded>().Single().Node.Id);
        _vaults.Write(plan);
        Assert.Equal("Dune", Target.FindNode(here.Id)!.Name);
        Assert.Null(Target.FindNode(dune.Id));
    }

    [Fact]
    public void ANodeOfTheSameName_WithNothingToChange_IsUnchanged_ItsLinksLeadingToIt()
    {
        var (books, dune) = (TwoVaults.Add(Source, "Books"), TwoVaults.Add(Source, "Dune"));
        var here = TwoVaults.Add(Target, "Dune");
        Assert.Equal(["same as a node here, found by its name"], Assert.Single(_vaults.Plan([dune]).Of(RecordOutcome.Unchanged)).Details);

        TwoVaults.Link(Source, books, dune);
        var plan = _vaults.Plan([books, dune]);
        _vaults.Write(plan);

        Assert.Empty(plan.Of(RecordOutcome.ToCheck));
        Assert.Equal(["same as a node here, found by its name", "contained by: + Books"], plan.Items[1].Details);
        Assert.NotNull(Target.FindLink(TypeCatalog.Contains.Id, books.Id, here.Id));
        Assert.Null(Target.FindNode(dune.Id));
    }

    [Fact]
    public void SeveralNodesOfTheName_OrTwoNodesForOne_AreNotImported()
    {
        var dune = TwoVaults.Add(Source, "Dune");
        TwoVaults.Add(Target, "Dune");
        TwoVaults.Add(Target, "Dune");
        var (solaris, other) = (TwoVaults.Add(Source, "Solaris"), TwoVaults.Add(Source, "solaris"));
        TwoVaults.Add(Target, "Solaris");

        var reasons = _vaults.Plan([dune, solaris, other]).Items.Select(item => item.Reason).ToList();

        Assert.Equal(["2 nodes of this name are here", "same node here as “solaris”", "same node here as “Solaris”"], reasons);
    }

    [Fact]
    public void ANodeOfAnotherType_IsNotFoundByItsName()
    {
        _vaults.DefineBook(Source);
        var dune = TwoVaults.Add(Source, "Dune", "book");
        TwoVaults.Add(Target, "Dune");

        Assert.Single(_vaults.Plan([dune]).Of(RecordOutcome.New));
    }

    [Fact]
    public void AView_AndATypeWhoseNameIsTaken_AreNotImported()
    {
        _vaults.DefineBook(Source);
        Target.Apply(new NodeTypeDefined(Guid.NewGuid(), TwoVaults.Start, "livre", "Book", null, null, [], []));
        var (view, dune) = (TwoVaults.Add(Source, "Shelf", "view"), TwoVaults.Add(Source, "Dune", "book"));

        var plan = _vaults.Plan([view, dune]);

        Assert.Equal(
            ["a view is not imported", "its type “Book” has the name of another type here: rename one first"],
            plan.Items.Select(item => item.Reason));
    }

    [Fact]
    public void WithTheSubtree_TheNodesUnderEachAreImported_Once()
    {
        var (books, sf, dune) = (TwoVaults.Add(Source, "Books"), TwoVaults.Add(Source, "SF"), TwoVaults.Add(Source, "Dune"));
        TwoVaults.Link(Source, books, sf);
        TwoVaults.Link(Source, sf, dune);

        Assert.Equal([sf, books, dune], _vaults.Plan([sf, books], TypeCatalog.Contains.Id).Items.Select(item => item.Source));
        Assert.Equal([sf], _vaults.Plan([sf, sf]).Items.Select(item => item.Source));
    }

    [Fact]
    public void AnExistingType_IsNotCopiedAgain()
    {
        _vaults.DefineBook(Source);
        _vaults.DefineBook(Target, "Livre");
        var dune = TwoVaults.Add(Source, "Dune", "book");

        var plan = _vaults.Plan([dune]);

        Assert.Empty(plan.NodeTypesAdded);
        Assert.Empty(plan.WriteFor(_ => RecordChoice.Import).Changes.OfType<NodeTypeDefined>());
        Assert.Equal("Livre", Target.Types.NodeTypeName("book"));
    }
}
