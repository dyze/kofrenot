namespace Kofrenot.Core.Tests.Views;

public sealed class GraphViewTests
{
    private readonly FamilyGraph _family = new();

    [Fact]
    public void AnUnknownRoot_HasNoGraph() =>
        Assert.Null(new GraphView(_family.Graph, new GraphDefinition(Guid.NewGuid(), 2, GraphLayoutKind.Layered, FamilyGraph.Family, [], [])).Read());

    [Fact]
    public void TheNodesReached_EitherWay_WithinTheDepth_AreShown_TheRootFirst_ThenByName()
    {
        var (me, mother, father, child, grandmother) = (_family.Add("Me"), _family.Add("Mother"), _family.Add("Father"), _family.Add("Child"), _family.Add("Grandmother"));
        _family.ParentOf(mother, me);
        _family.ParentOf(father, me);
        _family.ParentOf(me, child);
        _family.ParentOf(grandmother, mother);

        var content = _family.Read(_family.Definition(me, depth: 1));

        Assert.Equal([me, child, father, mother], content.Nodes);
        Assert.Same(me, content.Root);
        Assert.False(content.IsCut);
        Assert.Equal(3, content.Edges.Count);
        Assert.Contains(grandmother, _family.Read(_family.Definition(me, depth: 2)).Nodes);
    }

    [Fact]
    public void LinksOfOtherTypes_AreNotFollowed()
    {
        var (me, friend) = (_family.Add("Me"), _family.Add("Friend"));
        _family.Link(TypeCatalog.RelatesTo.Id, me, friend);

        var content = _family.Read(_family.Definition(me));

        Assert.Equal([me], content.Nodes);
        Assert.Empty(content.Edges);
    }

    [Fact]
    public void OnlyNodesOfTheTypesKept_AreShown_TheRootAlways()
    {
        var (me, note, mother) = (_family.Add("Me", TypeCatalog.Note.Id), _family.Add("Letters", TypeCatalog.Note.Id), _family.Add("Mother"));
        _family.ParentOf(mother, me);
        _family.ParentOf(mother, note);

        var content = _family.Read(_family.Definition(me, nodeTypes: [TypeCatalog.Generic.Id]));

        Assert.Equal([me, mother], content.Nodes);
    }

    [Fact]
    public void EachLinkBetweenShownNodes_IsDrawnOnce_WithItsRole_EvenAtTheEdgeOfTheDepth()
    {
        var (me, mother, father) = (_family.Add("Me"), _family.Add("Mother"), _family.Add("Father"));
        var (fromMother, fromFather, marriage) = (_family.ParentOf(mother, me), _family.ParentOf(father, me), _family.Married(mother, father));

        var content = _family.Read(_family.Definition(me, depth: 1));

        (Guid, GraphRole)[] expected = [(fromMother.Id, GraphRole.Down), (fromFather.Id, GraphRole.Down), (marriage.Id, GraphRole.Beside)];
        Assert.Equal(expected.OrderBy(edge => edge.Item1), content.Edges.Select(edge => (edge.Link.Id, edge.Role)).OrderBy(edge => edge.Id));
    }

    [Fact]
    public void ASymmetricTypeGoingDown_IsFree_ForItHasNoDirection()
    {
        var (me, friend) = (_family.Add("Me"), _family.Add("Friend"));
        _family.Married(me, friend);

        var content = _family.Read(_family.Definition(me, links: [new(FamilyGraph.Spouse, GraphRole.Down)]));

        Assert.Equal(GraphRole.Free, Assert.Single(content.Edges).Role);
    }

    [Fact]
    public void ALinkToItself_IsNotDrawn()
    {
        var me = _family.Add("Me");
        _family.ParentOf(me, me);

        Assert.Empty(_family.Read(_family.Definition(me)).Edges);
    }

    [Fact]
    public void ALinkTypeNamedTwice_KeepsItsFirstRole()
    {
        var (me, child) = (_family.Add("Me"), _family.Add("Child"));
        _family.ParentOf(me, child);

        var content = _family.Read(_family.Definition(me, links: [new(FamilyGraph.Parent, GraphRole.Up), new(FamilyGraph.Parent, GraphRole.Down)]));

        Assert.Equal(GraphRole.Up, Assert.Single(content.Edges).Role);
    }

    [Fact]
    public void BeyondTheMostNodes_TheGraphIsCut()
    {
        var root = _family.Add("Root");
        for (var index = 0; index < GraphView.MaxNodes + 1; index++)
        {
            _family.ParentOf(root, _family.Add($"Child {index}"));
        }

        var content = _family.Read(_family.Definition(root));

        Assert.True(content.IsCut);
        Assert.Equal(GraphView.MaxNodes, content.Nodes.Count);
        Assert.All(content.Edges, edge => Assert.Contains(content.Nodes, node => node.Id == edge.Link.To));
    }

    [Fact]
    public void ACard_ShowsTheValuesOfTheFieldsNamed_InTheViewsOrder()
    {
        var me = _family.Add("Me");
        Set(me, "Died", "2001");
        Set(me, "Born", "1920");
        Set(me, "Notes", "Long");
        var view = new GraphView(_family.Graph, _family.Definition(me, fields: ["born", "Died", "Height"]));

        Assert.Equal(["Born: 1920", "Died: 2001"], view.CardLinesOf(me));
    }

    private void Set(Node node, string field, string value) =>
        _family.Graph.Apply(new FieldSet(Guid.NewGuid(), TreeGraph.Start, node.Id, FieldSet.Own, field.ToLowerInvariant(), field, FieldKind.Text, value, []));
    [Fact]
    public void AnEdge_HasItsTypesColour_ItsDirection_AndItsName_AsReadFromItsFirstNode()
    {
        var (me, mother, father) = (_family.Add("Me"), _family.Add("Mother"), _family.Add("Father"));
        _family.ParentOf(mother, me);
        _family.Married(mother, father);

        var edges = _family.Read(_family.Definition(me)).Edges;

        var parent = edges.Single(edge => edge.Link.Type == FamilyGraph.Parent);
        var spouse = edges.Single(edge => edge.Link.Type == FamilyGraph.Spouse);
        Assert.Equal((0, true, "parent of"), (parent.Color, parent.IsDirected, parent.Name));
        Assert.Equal((1, false, "married to"), (spouse.Color, spouse.IsDirected, spouse.Name));
    }

    [Fact]
    public void TheLegend_NamesEachLinkTypeOnce_InTheViewsOrder_WithItsRole()
    {
        var me = _family.Add("Me");
        var view = new GraphView(_family.Graph, _family.Definition(me, links: [new("unknown", GraphRole.Free), new(FamilyGraph.Parent, GraphRole.Up), new(FamilyGraph.Parent, GraphRole.Down), new(FamilyGraph.Spouse, GraphRole.Down)]));

        Assert.Equal(
            [new GraphLegendItem("unknown", "unknown", GraphRole.Free, 0), new GraphLegendItem(FamilyGraph.Parent, "parent of", GraphRole.Up, 1), new GraphLegendItem(FamilyGraph.Spouse, "married to", GraphRole.Free, 2)],
            view.Legend);
        Assert.Same(view.Legend, view.Legend);
    }

    [Fact]
    public void Find_GivesTheShownNodesWhoseNameMatches_InTheOrderShown()
    {
        var (me, anne, annette, bob, outside) = (_family.Add("Me"), _family.Add("Anne"), _family.Add("Annette Smith"), _family.Add("Bob"), _family.Add("Anna"));
        _family.ParentOf(me, annette);
        _family.ParentOf(me, anne);
        _family.ParentOf(me, bob);

        var content = _family.Read(_family.Definition(me));

        Assert.Equal([anne, annette], content.Find("ann"));
        Assert.Equal([annette], content.Find("smi"));
        Assert.Empty(content.Find("zzz"));
        Assert.DoesNotContain(outside, content.Find("ann"));
    }

    [Fact]
    public void NodesPlacedInTheView_AreShown_UnlinkedOrNot_AfterThoseReached_ByName_WithTheirLinks()
    {
        var (me, child, zoe, alice, bob, elsewhere) = (_family.Add("Me"), _family.Add("Child"), _family.Add("Zoe"), _family.Add("Alice"), _family.Add("Bob"), _family.Add("Elsewhere"));
        _family.ParentOf(me, child);
        var marriage = _family.Married(alice, bob);
        var (view, other) = (Guid.NewGuid(), Guid.NewGuid());
        Place(view, child, zoe, alice, bob);
        Place(other, elsewhere);

        var content = new GraphView(_family.Graph, _family.Definition(me), view).Read()!;

        Assert.Equal([me, child, alice, bob, zoe], content.Nodes);
        Assert.Contains(content.Edges, edge => edge.Link == marriage);
        Assert.Equal([me, child], _family.Read(_family.Definition(me)).Nodes);
    }

    [Fact]
    public void NodesPlacedInTheView_AreKeptByItsNodeTypes_AndGoneWhenRemoved()
    {
        var (me, note, gone) = (_family.Add("Me"), _family.Add("Letters", TypeCatalog.Note.Id), _family.Add("Gone"));
        var view = Guid.NewGuid();
        Place(view, note, gone);
        _family.Graph.Apply(new ChangesRemoved(Guid.NewGuid(), TreeGraph.Start, [gone.Id]));

        var content = new GraphView(_family.Graph, _family.Definition(me, nodeTypes: [TypeCatalog.Generic.Id]), view).Read()!;

        Assert.Equal([me], content.Nodes);
    }

    [Fact]
    public void NodesPlacedInTheView_CountInTheMostNodes()
    {
        var root = _family.Add("Root");
        var view = Guid.NewGuid();
        for (var index = 0; index < GraphView.MaxNodes - 2; index++)
        {
            _family.ParentOf(root, _family.Add($"Child {index}"));
        }

        Place(view, _family.Add("Placed A"), _family.Add("Placed B"));

        var content = new GraphView(_family.Graph, _family.Definition(root), view).Read()!;

        Assert.True(content.IsCut);
        Assert.Equal(GraphView.MaxNodes, content.Nodes.Count);
        Assert.Equal("Placed A", content.Nodes[^1].Name);
    }

    private void Place(Guid view, params Node[] nodes) =>
        _family.Graph.Apply(new NodesPlaced(Guid.NewGuid(), TreeGraph.Start, view, [.. nodes.Select(node => new NodePosition(node.Id, 0, 0))]));
}
