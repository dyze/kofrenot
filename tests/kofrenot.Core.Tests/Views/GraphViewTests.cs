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
}
