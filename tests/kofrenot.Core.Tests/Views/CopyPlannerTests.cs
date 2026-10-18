namespace Kofrenot.Core.Tests.Views;

public sealed class CopyPlannerTests
{
    private readonly TreeGraph _tree = new();
    private readonly ManualClock _clock = new() { Now = TreeGraph.Start.AddHours(1) };
    private readonly CopyPlanner _planner;

    public CopyPlannerTests() => _planner = new CopyPlanner(_tree.Graph, TypeCatalog.Contains.Id, _clock);

    [Fact]
    public void ANode_IsCopied_WithItsSubtree_AsNewNodes_LinkedAsTheOriginals_UnderTheTarget()
    {
        var (projects, alpha, beta, shared, archive) = (_tree.Add("Projects"), _tree.Add("Alpha"), _tree.Add("Beta"), _tree.Add("Shared"), _tree.Add("Archive"));
        _tree.Contains(projects, alpha);
        _tree.Contains(projects, beta);
        _tree.Contains(alpha, shared);
        _tree.Contains(beta, shared);

        var plan = _planner.Plan(projects, archive, _ => true);
        _tree.Apply(plan.Changes);

        Assert.True(plan.CanCopy);
        Assert.Equal([alpha, beta, projects, shared], plan.Nodes);
        var tree = new TreeView(_tree.Graph, new TreeDefinition(archive.Id, TypeCatalog.Contains.Id));
        var copy = Assert.Single(tree.ChildrenOf(tree.Root!));
        Assert.NotEqual(projects.Id, copy.Node.Id);
        Assert.Equal(("Projects", _clock.Now), (copy.Node.Name, copy.Node.Created));
        var shown = tree.ChildrenOf(copy).Select(child => Assert.Single(tree.ChildrenOf(child)).Node).ToList();
        Assert.Equal("Shared", shown[0].Name);
        Assert.Same(shown[0], shown[1]);
        Assert.Equal((9, 9), (_tree.Graph.Nodes.Count, _tree.Graph.Links.Count));
    }

    [Fact]
    public void ACycle_IsCopiedOnce()
    {
        var (projects, alpha, archive) = (_tree.Add("Projects"), _tree.Add("Alpha"), _tree.Add("Archive"));
        _tree.Contains(projects, alpha);
        _tree.Contains(alpha, projects);

        var plan = _planner.Plan(projects, archive, _ => true);

        Assert.Equal(2, plan.Changes.OfType<NodeAdded>().Count());
        Assert.Equal(3, plan.Changes.OfType<LinkAdded>().Count());
    }

    [Fact]
    public void ANodeCopiedUnderItsOwnChild_KeepsTheOriginalChildAsTarget()
    {
        var (projects, alpha) = (_tree.Add("Projects"), _tree.Add("Alpha"));
        _tree.Contains(projects, alpha);

        var plan = _planner.Plan(projects, alpha, _ => true);
        _tree.Apply(plan.Changes);

        var copy = Assert.Single(_tree.Graph.NodesNamed("Projects"), node => node.Id != projects.Id);
        Assert.NotNull(_tree.Graph.FindLink(TypeCatalog.Contains.Id, alpha.Id, copy.Id));
    }

    [Fact]
    public void NotesDatasetsAndViews_StartWithTheLatestVersionOfTheirOriginal()
    {
        var (projects, ideas, survey, files, archive) = (
            _tree.Add("Projects"), _tree.Add("Ideas", TypeCatalog.Note.Id), _tree.Add("Survey", TypeCatalog.Dataset.Id), _tree.Add("Files", TypeCatalog.View.Id), _tree.Add("Archive"));
        _tree.Contains(projects, ideas);
        _tree.Contains(projects, survey);
        _tree.Contains(projects, files);
        var oldNote = new NoteEdited(Guid.NewGuid(), TreeGraph.Start, ideas.Id, []);
        var note = new NoteEdited(Guid.NewGuid(), TreeGraph.Start.AddMinutes(1), ideas.Id, [oldNote.Id]);
        var dataset = new DatasetEdited(Guid.NewGuid(), TreeGraph.Start, survey.Id, new DatasetFile("survey.csv", 4, new string('0', 64)), []);
        var view = new ViewEdited(Guid.NewGuid(), TreeGraph.Start, files.Id, new TreeDefinition(projects.Id, TypeCatalog.Contains.Id), []);
        _tree.Apply([oldNote, note, dataset, view]);

        var plan = _planner.Plan(projects, archive, _ => true);
        _tree.Apply(plan.Changes);

        Assert.Equal(2, plan.Contents.Count);
        var noteCopy = Assert.IsType<NoteEdited>(plan.Contents.Single(copy => copy.From == note).To);
        Assert.Equal([noteCopy], _tree.Graph.NoteVersionsOf(noteCopy.Node));
        var datasetCopy = Assert.IsType<DatasetEdited>(plan.Contents.Single(copy => copy.From == dataset).To);
        Assert.Equal(dataset.File, Assert.Single(_tree.Graph.DatasetVersionsOf(datasetCopy.Node)).File);
        var viewCopy = Assert.Single(plan.Changes.OfType<ViewEdited>());
        Assert.Equal(view.Definition, Assert.Single(_tree.Graph.ViewVersionsOf(viewCopy.Node)).Definition);
    }

    [Fact]
    public void ContentNotSynced_BlocksTheCopy()
    {
        var (projects, ideas, archive) = (_tree.Add("Projects"), _tree.Add("Ideas", TypeCatalog.Note.Id), _tree.Add("Archive"));
        _tree.Contains(projects, ideas);
        _tree.Apply([new NoteEdited(Guid.NewGuid(), TreeGraph.Start, ideas.Id, [])]);

        var plan = _planner.Plan(projects, archive, _ => false);

        Assert.False(plan.CanCopy);
        Assert.Equal([ideas], plan.Missing);
        Assert.Empty(plan.Contents);
    }
}
