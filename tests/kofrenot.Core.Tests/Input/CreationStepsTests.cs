namespace Kofrenot.Core.Tests.Input;

public sealed class CreationStepsTests
{
    private readonly ManualClock _clock = new();
    private readonly KnowledgeGraph _graph = new();

    [Fact]
    public void ANode_HasOneStep()
    {
        var form = Form(new NodeRequest("memo"));

        Assert.Equal([new CreationStep(Side.Left)], form.Steps);
        Assert.True(form.IsLastStep);
        Assert.Equal(string.Empty, form.StepText);
        Assert.Equal("memo", form.NameOf(Side.Left));
        Assert.False(form.Next());
        Assert.False(form.Back());
    }

    [Fact]
    public void ALinkBetweenNewNodes_HasAStepForEach_ThenTheLink()
    {
        var form = Form(new LinkRequest("memo", TypeCatalog.RelatesTo.Forward, "Dune"));

        Assert.Equal([new CreationStep(Side.Left), new CreationStep(Side.Right), new CreationStep(null)], form.Steps);
        Assert.Equal(("Step 1 of 3", "memo"), (form.StepText, form.NameOf(form.Step.Side!.Value)));

        Assert.True(form.Next());
        Assert.Equal(("Step 2 of 3", "Dune"), (form.StepText, form.NameOf(form.Step.Side!.Value)));

        Assert.True(form.Next());
        Assert.True(form.Step.IsLink);
        Assert.True(form.IsLastStep);
        Assert.False(form.Next());

        Assert.True(form.Back());
        Assert.Equal(1, form.StepIndex);
    }

    [Theory]
    [InlineData("memo", "Dune", Side.Left)]
    [InlineData("Dune", "memo", Side.Right)]
    public void AnExistingNode_HasNoStep(string left, string right, Side newSide)
    {
        Add("Dune");

        var form = Form(new LinkRequest(left, TypeCatalog.RelatesTo.Forward, right));

        Assert.Equal([new CreationStep(newSide), new CreationStep(null)], form.Steps);
    }

    [Fact]
    public void ALinkBetweenExistingNodes_HasTheLinkStepOnly()
    {
        Add("memo");
        Add("Dune");

        Assert.Equal([new CreationStep(null)], Form(new LinkRequest("memo", TypeCatalog.RelatesTo.Forward, "Dune")).Steps);
    }

    [Fact]
    public void EachSide_HasItsType()
    {
        var form = Form(new LinkRequest("memo", TypeCatalog.RelatesTo.Forward, "Dune"));

        form.SetType(Side.Left, TypeCatalog.Note);

        Assert.Equal((TypeCatalog.Note, TypeCatalog.Generic), (form.TypeOf(Side.Left), form.TypeOf(Side.Right)));
        Assert.True(form.IsNote(Side.Left));
        Assert.False(form.IsNote(Side.Right));

        form.SetType(Side.Right, TypeCatalog.Note);

        Assert.Equal(["note", "note"], form.Plan.Changes.OfType<NodeAdded>().Select(added => added.Node.Type));
    }

    [Fact]
    public void TheContentOfANewNote_IsSavedWithItsNode()
    {
        var form = Form(new LinkRequest("memo", TypeCatalog.RelatesTo.Forward, "Dune"));
        form.SetType(Side.Right, TypeCatalog.Note);
        form.SetContent(Side.Right, "# Dune");

        var note = Assert.Single(form.NotesToSave());

        var dune = form.Plan.Changes.OfType<NodeAdded>().Single(added => added.Node.Name == "Dune").Node;
        Assert.Equal((dune.Id, _clock.Now, "# Dune"), (note.Version.Node, note.Version.At, note.Content));
        Assert.Empty(note.Version.Replaces);
        Assert.Equal("# Dune", form.ContentOf(Side.Right));
    }

    [Fact]
    public void ContentIsSavedAndKept_OnlyForANoteWithText()
    {
        var form = Form(new NodeRequest("memo"));
        form.SetContent(Side.Left, "draft");

        Assert.False(form.HasContent);
        Assert.Empty(form.NotesToSave());

        form.SetType(Side.Left, TypeCatalog.Note);
        Assert.True(form.HasContent);
        Assert.Single(form.NotesToSave());

        form.SetContent(Side.Left, "  \n ");
        Assert.False(form.HasContent);
        Assert.Empty(form.NotesToSave());
    }

    [Fact]
    public void ANewNoteAndAnExistingNode_SaveTheRightNodesContent()
    {
        Add("memo");
        var form = Form(new LinkRequest("memo", TypeCatalog.RelatesTo.Forward, "Dune"));
        form.SetType(Side.Right, TypeCatalog.Note);
        form.SetContent(Side.Right, "text");

        var dune = Assert.Single(form.Plan.Changes.OfType<NodeAdded>()).Node;
        Assert.Equal(dune.Id, Assert.Single(form.NotesToSave()).Version.Node);
    }

    [Fact]
    public void TheFileOfANewDataset_IsImportedWithItsNode()
    {
        var form = Form(new LinkRequest("memo", TypeCatalog.RelatesTo.Forward, "Survey"));
        form.SetType(Side.Right, TypeCatalog.Dataset);
        var chosen = new ChosenFile(@"C:\data\survey.csv", new DatasetFile("survey.csv", 10, new string('a', 64)));
        form.SetFile(Side.Right, chosen);

        var dataset = Assert.Single(form.DatasetsToSave());

        var survey = form.Plan.Changes.OfType<NodeAdded>().Single(added => added.Node.Name == "Survey").Node;
        Assert.Equal((survey.Id, _clock.Now, chosen.File, chosen.Path), (dataset.Version.Node, dataset.Version.At, dataset.Version.File, dataset.Path));
        Assert.Empty(dataset.Version.Replaces);
        Assert.Empty(form.NotesToSave());
        Assert.True(form.HasContent);
    }

    [Fact]
    public void AFile_IsImportedAndKept_OnlyForADataset_UntilTakenBack()
    {
        var form = Form(new NodeRequest("survey"));
        var chosen = new ChosenFile("survey.csv", new DatasetFile("survey.csv", 10, new string('a', 64)));
        form.SetType(Side.Left, TypeCatalog.Dataset);
        form.SetFile(Side.Left, chosen);

        form.SetType(Side.Left, TypeCatalog.Note);
        Assert.False(form.IsDataset(Side.Left));
        Assert.Null(form.FileOf(Side.Left));
        Assert.False(form.HasContent);
        Assert.Empty(form.DatasetsToSave());

        form.SetType(Side.Left, TypeCatalog.Dataset);
        Assert.Equal(chosen, form.FileOf(Side.Left));
        Assert.Single(form.DatasetsToSave());

        form.SetFile(Side.Left, null);
        Assert.Null(form.FileOf(Side.Left));
        Assert.Empty(form.DatasetsToSave());
    }

    private CreationForm Form(InputRequest request) => new(_graph, request, clock: _clock);

    private void Add(string name) => _graph.Add(new Node(Guid.NewGuid(), "generic", name, _clock.Now));
}
