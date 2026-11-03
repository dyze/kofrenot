namespace Kofrenot.Core.Tests.Views;

public sealed class GraphPlacerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();
    private readonly Guid _view = Guid.NewGuid();
    private readonly GraphPlacer _placer;

    public GraphPlacerTests() => _placer = new GraphPlacer(_graph, new ManualClock { Now = Now });

    [Fact]
    public void AMove_IsKept_InWholeUnits()
    {
        var node = Guid.NewGuid();

        var placed = _placer.Move(_view, [new NodePosition(node, 10.4, -20.6)])!;

        Assert.Equal((_view, Now), (placed.View, placed.At));
        Assert.Equal([new NodePosition(node, 10, -21)], placed.Positions);
    }

    [Fact]
    public void NodesNotMovedFromWhereTheyWereKept_AreNotWrittenAgain()
    {
        var (kept, moved) = (Guid.NewGuid(), Guid.NewGuid());
        _graph.Apply(_placer.Move(_view, [new NodePosition(kept, 1, 2)])!);

        Assert.Null(_placer.Move(_view, [new NodePosition(kept, 1.2, 2)]));
        Assert.Equal([new NodePosition(moved, 3, 4)], _placer.Move(_view, [new NodePosition(kept, 1, 2), new NodePosition(moved, 3, 4)])!.Positions);
    }

    [Fact]
    public void AReset_RemovesEveryPlacementOfTheView_AndNoOther()
    {
        var node = Guid.NewGuid();
        var first = _placer.Move(_view, [new NodePosition(node, 1, 2)])!;
        _graph.Apply(first);
        var second = _placer.Move(_view, [new NodePosition(node, 3, 4)])! with { Id = Guid.NewGuid() };
        _graph.Apply(second);
        var other = Guid.NewGuid();
        _graph.Apply(_placer.Move(other, [new NodePosition(node, 5, 6)])!);

        var reset = _placer.Reset(_view)!;
        _graph.Apply(reset);

        Assert.Equal(1, _placer.MovedCount(other));
        Assert.Equal(new[] { first.Id, second.Id }.Order(), reset.Removes.Order());
        Assert.Empty(_graph.PositionsIn(_view));
        Assert.Equal(0, _placer.MovedCount(_view));
        Assert.Null(_placer.Reset(_view));
    }
}
