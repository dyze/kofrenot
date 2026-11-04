namespace Kofrenot.Core.Tests.Views;

public sealed class CardOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();
    private readonly Guid _view = Guid.NewGuid();
    private readonly CardOrder _order;
    private readonly Node _a = Node("A");
    private readonly Node _b = Node("B");
    private readonly Node _c = Node("C");

    public CardOrderTests() => _order = new CardOrder(_graph, _view, new ManualClock { Now = Now });

    [Fact]
    public void CardsNotArranged_KeepTheOrderGiven()
    {
        Assert.Equal([_a, _b, _c], _order.Of([_a, _b, _c]));
    }

    [Fact]
    public void PinnedCards_ComeFirst()
    {
        Pin(_c);

        Assert.True(_order.IsPinned(_c));
        Assert.False(_order.IsPinned(_a));
        Assert.Equal([_c, _a, _b], _order.Of([_a, _b, _c]));
    }

    [Fact]
    public void AMove_RanksEveryCardByItsNewPlace_AndIsKept()
    {
        var placed = _order.Move([_a, _b, _c], from: 2, to: 0)!;

        Assert.Equal((_view, Now), (placed.View, placed.At));
        Assert.Equal([new NodePosition(_c.Id, 0, 0), new NodePosition(_a.Id, 0, 1), new NodePosition(_b.Id, 0, 2)], placed.Positions);

        _graph.Apply(placed);
        Assert.Equal([_c, _a, _b], _order.Of([_a, _b, _c]));
    }

    [Fact]
    public void CardsNotMovedFromTheirRank_AreNotWrittenAgain()
    {
        _graph.Apply(_order.Move([_a, _b, _c], from: 0, to: 1)!);

        var placed = _order.Move([_b, _a, _c], from: 2, to: 1)!;

        Assert.Equal([new NodePosition(_c.Id, 0, 1), new NodePosition(_a.Id, 0, 2)], placed.Positions);
    }

    [Fact]
    public void ArrangedCards_ComeBeforeThoseNotArranged_InTheirPart()
    {
        var d = Node("D");
        _graph.Apply(_order.Move([_a, _b], from: 1, to: 0)!);

        Assert.Equal([_b, _a, _c, d], _order.Of([_a, _b, _c, d]));
    }

    [Fact]
    public void ACard_StaysInItsPart()
    {
        Pin(_a);

        Assert.Null(_order.Move([_a, _b, _c], from: 0, to: 2));
        Assert.Null(_order.Move([_a, _b, _c], from: 1, to: 1));
        Assert.NotNull(_order.Move([_a, _b, _c], from: 2, to: 1));
    }

    [Fact]
    public void AnotherViewsRanks_DoNotCount()
    {
        _graph.Apply(new CardOrder(_graph, Guid.NewGuid()).Move([_a, _b], from: 1, to: 0)!);

        Assert.Equal([_a, _b], _order.Of([_a, _b]));
    }

    private void Pin(Node node) => _graph.Apply(new NodePinned(Guid.NewGuid(), Now, node.Id));

    private static Node Node(string name) => new(Guid.NewGuid(), TypeCatalog.Note.Id, name, Now);
}
