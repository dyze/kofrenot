namespace Kofrenot.Core.Tests.Graph;

public sealed class PlacementTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    private readonly Guid _view = Guid.NewGuid();
    private readonly Guid _node = Guid.NewGuid();

    [Fact]
    public void ANodeNeverMoved_HasNoPosition() =>
        Assert.Empty(new KnowledgeGraph().PositionsIn(_view));

    [Fact]
    public void TheNewestMove_Wins_WhateverTheOrderTheyArrive()
    {
        var older = Place(minutes: 0, x: 1);
        var newer = Place(minutes: 5, x: 2);

        foreach (var order in new[] { new[] { older, newer }, [newer, older] })
        {
            var graph = new KnowledgeGraph();
            Assert.All(order, change => Assert.True(graph.Apply(change)));

            Assert.Equal(2, graph.PositionsIn(_view)[_node].X);
            Assert.Equal([older, newer], graph.PlacementsOf(_view));
        }
    }

    [Fact]
    public void AMoveAppliedTwice_ChangesNothing()
    {
        var graph = new KnowledgeGraph();
        var placed = Place(minutes: 0, x: 1);

        Assert.True(graph.Apply(placed));
        Assert.False(graph.Apply(placed));
    }

    [Fact]
    public void PositionsAreKeptPerView()
    {
        var graph = new KnowledgeGraph();
        graph.Apply(Place(minutes: 0, x: 1));

        Assert.Empty(graph.PositionsIn(Guid.NewGuid()));
        Assert.Empty(graph.PlacementsOf(Guid.NewGuid()));
    }

    [Fact]
    public void ARemovedMove_GivesTheNodeBackItsOlderPosition_EvenWhenTheRemovalArrivesFirst()
    {
        var (older, newer) = (Place(minutes: 0, x: 1), Place(minutes: 5, x: 2));
        var graph = new KnowledgeGraph();

        graph.Apply(new ChangesRemoved(Guid.NewGuid(), Start, [newer.Id]));
        graph.Apply(newer);
        graph.Apply(older);

        Assert.Equal(1, graph.PositionsIn(_view)[_node].X);
    }

    [Fact]
    public void Placements_AreEqualByValue()
    {
        var placed = Place(minutes: 0, x: 1);
        var same = placed with { Positions = [.. placed.Positions] };

        Assert.Equal(placed, same);
        Assert.Equal(placed.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(placed, same with { Positions = [new NodePosition(_node, 9, 0)] });
        Assert.False(placed.Equals(null));
    }

    private NodesPlaced Place(int minutes, double x) =>
        new(Guid.NewGuid(), Start.AddMinutes(minutes), _view, [new NodePosition(_node, x, 0)]);
}
