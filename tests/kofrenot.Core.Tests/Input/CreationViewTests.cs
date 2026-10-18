namespace Kofrenot.Core.Tests.Input;

public sealed class CreationViewTests
{
    private readonly ManualClock _clock = new();
    private readonly KnowledgeGraph _graph = new();

    [Fact]
    public void AViewStep_StartsAsATreeThroughContains_WithoutRoot_AndCannotBeAdded()
    {
        var form = ViewForm(new NodeRequest("Files"));

        Assert.True(form.IsView(Side.Left));
        Assert.Equal(ViewDraft.Default, form.ViewOf(Side.Left));
        Assert.Null(form.ViewOf(Side.Left).Definition);
        Assert.False(form.CanLeaveStep);
        Assert.False(form.CanAdd);
        Assert.Empty(form.ViewsToSave());
    }

    [Fact]
    public void ATreeWithItsRoot_IsSavedWithTheNewView()
    {
        var projects = Add("Projects");
        var form = ViewForm(new NodeRequest("Files"));

        var root = Assert.Single(form.FindRoots("proj")).Node;
        form.SetView(Side.Left, form.ViewOf(Side.Left) with { Root = root });

        Assert.True(form.CanAdd);
        var version = Assert.Single(form.ViewsToSave());
        Assert.Equal(form.Plan.NewNodes[0].Id, version.Node);
        Assert.Equal(new TreeDefinition(projects.Id, TypeCatalog.Contains.Id), version.Definition);
        Assert.Equal((_clock.Now, 0), (version.At, version.Replaces.Count));
    }

    [Fact]
    public void AList_NeedsNoRoot()
    {
        var form = ViewForm(new NodeRequest("Memos"));

        form.SetView(Side.Left, form.ViewOf(Side.Left) with { Display = ViewDisplay.Flat, NodeType = TypeCatalog.Note });

        Assert.True(form.CanLeaveStep);
        Assert.Equal(new FlatDefinition(TypeCatalog.Note.Id), Assert.Single(form.ViewsToSave()).Definition);
    }

    [Fact]
    public void AViewStep_IsLeftOnlyOnceDefined()
    {
        var form = new CreationForm(_graph, new LinkRequest("Files", TypeCatalog.RelatesTo.Forward, "Dune"), clock: _clock);
        form.SetType(Side.Left, TypeCatalog.View);

        Assert.False(form.Next());

        form.SetView(Side.Left, form.ViewOf(Side.Left) with { Display = ViewDisplay.Flat });
        Assert.True(form.Next());
        Assert.True(form.CanLeaveStep);
    }

    [Fact]
    public void AnotherType_SavesNoView()
    {
        var form = new CreationForm(_graph, new NodeRequest("Dune"), clock: _clock);
        form.SetView(Side.Left, form.ViewOf(Side.Left) with { Display = ViewDisplay.Flat });

        Assert.False(form.IsView(Side.Left));
        Assert.True(form.CanAdd);
        Assert.Empty(form.ViewsToSave());
    }

    private CreationForm ViewForm(InputRequest request)
    {
        var form = new CreationForm(_graph, request, clock: _clock);
        form.SetType(Side.Left, TypeCatalog.View);
        return form;
    }

    private Node Add(string name)
    {
        var node = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, name, _clock.Now);
        _graph.Add(node);
        return node;
    }
}
