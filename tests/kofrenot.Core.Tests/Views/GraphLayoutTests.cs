namespace Kofrenot.Core.Tests.Views;

public sealed class GraphLayoutTests
{
    private const double RankHeight = GraphLayout.CardHeight + GraphLayout.RankGap;

    private readonly FamilyGraph _family = new();

    [Fact]
    public void Layered_PutsParentsARankAboveTheirChildren_SideBySide()
    {
        var (mother, first, second) = (_family.Add("Mother"), _family.Add("Anna"), _family.Add("Ben"));
        _family.ParentOf(mother, first);
        _family.ParentOf(mother, second);

        var at = Layout(mother);

        Assert.Equal(at[mother.Id].Y + RankHeight, at[first.Id].Y);
        Assert.Equal(at[first.Id].Y, at[second.Id].Y);
        Assert.True(at[first.Id].X < at[second.Id].X);
        Assert.Equal(at[mother.Id].X, (at[first.Id].X + at[second.Id].X) / 2, precision: 6);
        AssertNoOverlap(at);
    }

    [Fact]
    public void Layered_KeepsSpousesOnOneRank_NextToEachOther()
    {
        var (me, wife, child, wifesMother) = (_family.Add("Me"), _family.Add("Wife"), _family.Add("Child"), _family.Add("Her mother"));
        _family.Married(me, wife);
        _family.ParentOf(me, child);
        _family.ParentOf(wifesMother, wife);

        var at = Layout(me);

        Assert.Equal(at[me.Id].Y, at[wife.Id].Y);
        Assert.Equal(GraphLayout.CardWidth + GraphLayout.ColumnGap, Math.Abs(at[me.Id].X - at[wife.Id].X), precision: 6);
        Assert.Equal(at[me.Id].Y + RankHeight, at[child.Id].Y);
        Assert.Equal(at[wife.Id].Y - RankHeight, at[wifesMother.Id].Y);
        AssertNoOverlap(at);
    }

    [Fact]
    public void Layered_PutsAParentWithNoParentJustAboveItsChild()
    {
        var (grandfather, father, me, mother) = (_family.Add("Grandfather"), _family.Add("Father"), _family.Add("Me"), _family.Add("Mother"));
        _family.ParentOf(grandfather, father);
        _family.ParentOf(father, me);
        _family.ParentOf(mother, me);

        var at = Layout(me);

        Assert.Equal(at[father.Id].Y, at[mother.Id].Y);
        Assert.Equal(0, at[grandfather.Id].Y - (GraphLayout.CardHeight / 2));
    }

    [Fact]
    public void Layered_ReadsUpLinks_FromTheChild()
    {
        var (me, mother) = (_family.Add("Me"), _family.Add("Mother"));
        _family.Link(FamilyGraph.Parent, me, mother);

        var at = Layout(me, [new(FamilyGraph.Parent, GraphRole.Up)]);

        Assert.Equal(at[mother.Id].Y + RankHeight, at[me.Id].Y);
    }

    [Fact]
    public void Layered_CutsACycle_WhereItCloses()
    {
        var (first, second, third) = (_family.Add("First"), _family.Add("Second"), _family.Add("Third"));
        _family.ParentOf(first, second);
        _family.ParentOf(second, third);
        _family.ParentOf(third, first);

        var at = Layout(first);

        Assert.Equal(3, at.Values.Select(position => position.Y).Distinct().Count());
        AssertNoOverlap(at);
    }

    [Fact]
    public void Layered_PutsANodeLinkedFreely_OnItsNeighboursRank()
    {
        var (mother, me, friend) = (_family.Add("Mother"), _family.Add("Me"), _family.Add("Friend"));
        _family.ParentOf(mother, me);
        _family.Link(TypeCatalog.RelatesTo.Id, me, friend);

        var at = Layout(me, [.. FamilyGraph.Family, new(TypeCatalog.RelatesTo.Id, GraphRole.Free)]);

        Assert.Equal(at[me.Id].Y, at[friend.Id].Y);
        AssertNoOverlap(at);
    }

    [Fact]
    public void Layered_OrdersARank_ToCrossFewerLinks()
    {
        var (root, left, right) = (_family.Add("Root"), _family.Add("A"), _family.Add("B"));
        var (underRight, underLeft) = (_family.Add("C"), _family.Add("D"));
        _family.ParentOf(root, left);
        _family.ParentOf(root, right);
        _family.ParentOf(right, underRight);
        _family.ParentOf(left, underLeft);

        var at = Layout(root);

        Assert.Equal(at[left.Id].X < at[right.Id].X, at[underLeft.Id].X < at[underRight.Id].X);
    }

    [Fact]
    public void Network_IsTheSameEachTime_WithNoCardOverAnother()
    {
        var root = _family.Add("Root");
        foreach (var name in new[] { "A", "B", "C", "D", "E" })
        {
            _family.Link(TypeCatalog.RelatesTo.Id, root, _family.Add(name));
        }

        var links = new GraphLink[] { new(TypeCatalog.RelatesTo.Id, GraphRole.Free) };
        var first = Layout(root, links, GraphLayoutKind.Network);

        Assert.Equal(first, Layout(root, links, GraphLayoutKind.Network));
        Assert.Equal(6, first.Count);
        AssertNoOverlap(first);
    }

    [Fact]
    public void Network_PullsLinkedNodesCloser_ThanOthers()
    {
        var (root, linked, apart) = (_family.Add("Root"), _family.Add("Linked"), _family.Add("Apart"));
        _family.Link(TypeCatalog.RelatesTo.Id, root, linked);
        _family.Link(TypeCatalog.RelatesTo.Id, linked, apart);

        var at = Layout(root, [new(TypeCatalog.RelatesTo.Id, GraphRole.Free)], GraphLayoutKind.Network);

        Assert.True(Distance(at[root.Id], at[linked.Id]) < Distance(at[root.Id], at[apart.Id]));
    }

    [Theory]
    [InlineData(GraphLayoutKind.Layered)]
    [InlineData(GraphLayoutKind.Network)]
    public void NodesMovedByHand_StayWhereTheyWere_AndTheOthersGoAroundThem(GraphLayoutKind kind)
    {
        var (mother, first, second) = (_family.Add("Mother"), _family.Add("Anna"), _family.Add("Ben"));
        _family.ParentOf(mother, first);
        _family.ParentOf(mother, second);
        var content = _family.Read(_family.Definition(mother, layout: kind));
        var free = GraphLayout.Of(content, kind, new Dictionary<Guid, NodePosition>());
        var onSecond = new NodePosition(first.Id, free[second.Id].X, free[second.Id].Y);
        var elsewhere = new NodePosition(Guid.NewGuid(), 0, 0);

        var at = GraphLayout.Of(content, kind, new Dictionary<Guid, NodePosition> { [first.Id] = onSecond, [elsewhere.Node] = elsewhere });

        Assert.Equal(onSecond, at[first.Id]);
        Assert.False(at.ContainsKey(elsewhere.Node));
        AssertNoOverlap(at);
    }

    [Fact]
    public void ARootAlone_IsPlaced()
    {
        var root = _family.Add("Root");

        Assert.Single(Layout(root));
        Assert.Single(Layout(root, kind: GraphLayoutKind.Network));
    }

    [Fact]
    public void TheBounds_CoverEveryCard_WholeCardsIncluded()
    {
        Assert.Null(GraphLayout.BoundsOf([]));

        var bounds = GraphLayout.BoundsOf([new(Guid.NewGuid(), 0, 0), new(Guid.NewGuid(), 300, -100)])!;

        Assert.Equal(new GraphBounds(-GraphLayout.CardWidth / 2, -100 - (GraphLayout.CardHeight / 2), 300 + (GraphLayout.CardWidth / 2), GraphLayout.CardHeight / 2), bounds);
        Assert.Equal(300 + GraphLayout.CardWidth, bounds.Width);
        Assert.Equal(100 + GraphLayout.CardHeight, bounds.Height);
    }

    [Fact]
    public void ALine_LeavesACard_OnItsBorder()
    {
        var card = new NodePosition(Guid.NewGuid(), 100, 100);

        Assert.Equal((100 + (GraphLayout.CardWidth / 2), 100), GraphLayout.BorderPoint(card, 1000, 100));
        Assert.Equal((100, 100 - (GraphLayout.CardHeight / 2)), GraphLayout.BorderPoint(card, 100, -500));
        Assert.Equal((100 + (GraphLayout.CardHeight / 2), 100 + (GraphLayout.CardHeight / 2)), GraphLayout.BorderPoint(card, 200, 200));
        Assert.Equal((100, 100), GraphLayout.BorderPoint(card, 110, 105));
    }

    [Fact]
    public void TheCardAtAPoint_IsTheLastOneCoveringIt()
    {
        var (below, above) = (new NodePosition(Guid.NewGuid(), 0, 0), new NodePosition(Guid.NewGuid(), 50, 0));

        Assert.Equal(below.Node, GraphLayout.CardAt([below, above], -GraphLayout.CardWidth / 2, 0));
        Assert.Equal(above.Node, GraphLayout.CardAt([below, above], 10, GraphLayout.CardHeight / 2));
        Assert.Null(GraphLayout.CardAt([below, above], 0, GraphLayout.CardHeight));
    }

    private IReadOnlyDictionary<Guid, NodePosition> Layout(Node root, IReadOnlyList<GraphLink>? links = null, GraphLayoutKind kind = GraphLayoutKind.Layered) =>
        GraphLayout.Of(_family.Read(_family.Definition(root, layout: kind, links: links)), kind, new Dictionary<Guid, NodePosition>());

    private static double Distance(NodePosition first, NodePosition second) => Math.Sqrt(Math.Pow(first.X - second.X, 2) + Math.Pow(first.Y - second.Y, 2));

    private static void AssertNoOverlap(IReadOnlyDictionary<Guid, NodePosition> positions)
    {
        var all = positions.Values.ToList();
        for (var first = 0; first < all.Count; first++)
        {
            for (var second = first + 1; second < all.Count; second++)
            {
                Assert.False(Math.Abs(all[first].X - all[second].X) < GraphLayout.CardWidth && Math.Abs(all[first].Y - all[second].Y) < GraphLayout.CardHeight, $"{all[first]} covers {all[second]}");
            }
        }
    }
}
