namespace Kofrenot.Core.Tests.Views;

public sealed class ViewDraftTests
{
    private readonly TreeGraph _tree = new();

    [Fact]
    public void ATree_IsEditedWithItsRootAndLinkType()
    {
        var projects = _tree.Add("Projects");

        var draft = ViewDraft.Of(new TreeDefinition(projects.Id, TypeCatalog.IsA.Id), _tree.Graph);

        Assert.Equal((ViewDisplay.Tree, projects, TypeCatalog.IsA), (draft.Display, draft.Root, draft.LinkType));
        Assert.Equal(new TreeDefinition(projects.Id, TypeCatalog.IsA.Id), draft.Definition);
    }

    [Fact]
    public void ATreeWhoseRootIsGone_HasItsRootToChooseAgain()
    {
        var draft = ViewDraft.Of(new TreeDefinition(Guid.NewGuid(), TypeCatalog.Contains.Id), _tree.Graph);

        Assert.Null(draft.Root);
        Assert.Null(draft.Definition);
    }

    [Fact]
    public void AList_IsEditedWithItsNodeType()
    {
        var draft = ViewDraft.Of(new FlatDefinition(TypeCatalog.Dataset.Id), _tree.Graph);

        Assert.Equal((ViewDisplay.Flat, TypeCatalog.Dataset), (draft.Display, draft.NodeType));
        Assert.Equal(new FlatDefinition(TypeCatalog.Dataset.Id), draft.Definition);
    }

    [Fact]
    public void TypesThisVersionDoesNotKnow_AreKeptByTheirId()
    {
        var root = _tree.Add("Projects");

        Assert.Equal(new TreeDefinition(root.Id, "part-of"), ViewDraft.Of(new TreeDefinition(root.Id, "part-of"), _tree.Graph).Definition);
        Assert.Equal(new FlatDefinition("book"), ViewDraft.Of(new FlatDefinition("book"), _tree.Graph).Definition);
    }

    [Fact]
    public void AnUnknownDefinition_IsEditedFromTheDefault() =>
        Assert.Equal(ViewDraft.Default, ViewDraft.Of(new OtherDefinition(), _tree.Graph));

    [Fact]
    public void Roots_AreFoundByName()
    {
        var projects = _tree.Add("Projects");
        _tree.Add("Dune");

        Assert.Equal([projects], ViewDraft.FindRoots(_tree.Graph, "proj").Select(hit => hit.Node));
    }

    [Fact]
    public void AView_IsDescribedInASentence()
    {
        var projects = _tree.Add("Projects");

        Assert.Equal("A tree from “Projects” through “contains”", ViewDraft.Describe(new TreeDefinition(projects.Id, TypeCatalog.Contains.Id), _tree.Graph));
        Assert.Equal("A tree from “Projects” through “part-of”", ViewDraft.Describe(new TreeDefinition(projects.Id, "part-of"), _tree.Graph));
        Assert.Equal("A tree from a node not here: removed, or not synced yet", ViewDraft.Describe(new TreeDefinition(Guid.NewGuid(), TypeCatalog.Contains.Id), _tree.Graph));
        Assert.Equal("A list of every Note", ViewDraft.Describe(new FlatDefinition(TypeCatalog.Note.Id), _tree.Graph));
        Assert.Equal("No definition yet", ViewDraft.Describe(null, _tree.Graph));
    }

    [Fact]
    public void AGraph_IsEditedWithItsRootAndSettings()
    {
        var me = _tree.Add("Me");
        var definition = new GraphDefinition(me.Id, 4, GraphLayoutKind.Network, [new("parent", GraphRole.Down)], ["person"], ["Born"]);

        var draft = ViewDraft.Of(definition, _tree.Graph);

        Assert.Equal((ViewDisplay.Graph, me), (draft.Display, draft.Root));
        Assert.Equal(new GraphDraft(4, GraphLayoutKind.Network, definition.Links, definition.NodeTypes, definition.Fields), draft.Graph);
        Assert.Equal(definition, draft.Definition);
    }

    [Fact]
    public void AGraph_NeedsItsRoot_AndALinkType()
    {
        var me = _tree.Add("Me");
        var draft = ViewDraft.Default with { Display = ViewDisplay.Graph };

        Assert.Null(draft.Definition);
        Assert.Null((draft with { Root = me }).Definition);
        Assert.Null((draft with { Graph = draft.Graph.WithRole("parent", GraphRole.Down) }).Definition);
        Assert.Equal(
            new GraphDefinition(me.Id, GraphDraft.DefaultDepth, GraphLayoutKind.Layered, [new("parent", GraphRole.Down)], [], []),
            (draft with { Root = me, Graph = draft.Graph.WithRole("parent", GraphRole.Down) }).Definition);
    }

    [Fact]
    public void AGraphsLinkTypes_KeepTheirPlace_WhenTheirRoleChanges()
    {
        var graph = GraphDraft.Default.WithRole("parent", GraphRole.Down).WithRole("spouse", GraphRole.Beside);

        Assert.Equal([new GraphLink("parent", GraphRole.Up), new GraphLink("spouse", GraphRole.Beside)], graph.WithRole("parent", GraphRole.Up).Links);
        Assert.Equal([new GraphLink("spouse", GraphRole.Beside)], graph.WithRole("parent", null).Links);
        Assert.Equal(GraphRole.Beside, graph.RoleOf("spouse"));
        Assert.Null(graph.RoleOf("friend"));
        Assert.Empty(GraphDraft.Default.WithRole("friend", null).Links);
    }

    [Fact]
    public void AGraphsNodeTypes_FieldsAndDepth_AreEdited()
    {
        var graph = GraphDraft.Default.WithNodeType("person", isKept: true).WithNodeType("book", isKept: true).WithNodeType("person", isKept: true);

        Assert.Equal(["book", "person"], graph.NodeTypes);
        Assert.Equal(["person"], graph.WithNodeType("book", isKept: false).NodeTypes);
        Assert.Equal(["Born", "Died"], GraphDraft.Default.WithFields(" Born, ,Died, born ").Fields);
        Assert.Equal(GraphDefinition.MaxDepth, GraphDraft.Default.WithDepth(99).Depth);
        Assert.Equal(GraphDefinition.MinDepth, GraphDraft.Default.WithDepth(0).Depth);
    }

    [Fact]
    public void AGraphWhoseRootIsGone_HasItsRootToChooseAgain()
    {
        var draft = ViewDraft.Of(new GraphDefinition(Guid.NewGuid(), 2, GraphLayoutKind.Layered, [new("parent", GraphRole.Down)], [], []), _tree.Graph);

        Assert.Null(draft.Root);
        Assert.Null(draft.Definition);
    }

    [Fact]
    public void AGraph_IsDescribedInASentence()
    {
        var me = _tree.Add("Me");
        GraphDefinition Graph(Guid root, int depth) => new(root, depth, GraphLayoutKind.Layered, [new(TypeCatalog.Contains.Id, GraphRole.Down), new("spouse", GraphRole.Beside)], [], []);

        Assert.Equal("A graph from “Me” through “contains”, “spouse”, 2 steps away", ViewDraft.Describe(Graph(me.Id, 2), _tree.Graph));
        Assert.Equal("A graph from “Me” through “contains”, “spouse”, 1 step away", ViewDraft.Describe(Graph(me.Id, 1), _tree.Graph));
        Assert.Equal("A graph from a node not here: removed, or not synced yet", ViewDraft.Describe(Graph(Guid.NewGuid(), 1), _tree.Graph));
    }

    private sealed record OtherDefinition : ViewDefinition;
}
