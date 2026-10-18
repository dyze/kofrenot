namespace Kofrenot.Core.Tests.Graph;

public sealed class KnowledgeGraphTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();

    [Fact]
    public void AnAddedNode_IsFound()
    {
        var dune = NewNode("Dune");

        Assert.True(_graph.Apply(Added(dune)));

        Assert.Same(dune, _graph.FindNode(dune.Id));
        Assert.Equal([dune], _graph.NodesNamed("  DUNE "));
        Assert.Single(_graph.Nodes);
    }

    [Fact]
    public void ApplyingAChangeTwice_ChangesNothing()
    {
        var change = Added(NewNode("Dune"));
        _graph.Apply(change);

        Assert.False(_graph.Apply(change));
        Assert.Single(_graph.Nodes);
    }

    [Fact]
    public void AnUnknownName_HasNoNode() =>
        Assert.Empty(_graph.NodesNamed("Dune"));

    [Fact]
    public void Duplicates_AreAllKept_OldestFirst_WhateverTheOrderTheyArrive()
    {
        var newer = NewNode("Dune", minutes: 5);
        var older = NewNode("dune", minutes: 1);

        _graph.Apply(Added(newer));
        _graph.Apply(Added(older));

        Assert.Equal([older, newer], _graph.NodesNamed("Dune"));
    }

    [Fact]
    public void ALink_IsListedOnBothNodes()
    {
        var (dune, herbert) = (NewNode("Dune"), NewNode("Frank Herbert"));
        Apply(dune, herbert);
        var link = NewLink(dune, herbert);

        Assert.True(_graph.Apply(Added(link)));

        Assert.Equal([link], _graph.LinksOf(dune.Id));
        Assert.Equal([link], _graph.LinksOf(herbert.Id));
        Assert.Equal(herbert.Id, link.Other(dune.Id));
        Assert.Equal(dune.Id, link.Other(herbert.Id));
        Assert.Single(_graph.Links);
    }

    [Fact]
    public void ASymmetricLink_IsFoundBothWays()
    {
        var (dune, herbert) = (NewNode("Dune"), NewNode("Frank Herbert"));
        Apply(dune, herbert);
        var link = NewLink(dune, herbert);
        _graph.Apply(Added(link));

        Assert.Same(link, _graph.FindLink(TypeCatalog.RelatesTo.Id, herbert.Id, dune.Id));
    }

    [Fact]
    public void ADirectedLink_IsFoundInItsDirectionOnly()
    {
        var graph = new KnowledgeGraph(new TypeCatalog([TypeCatalog.Generic], [new LinkType("written-by", ["written by"], ["wrote"])]));
        var (dune, herbert) = (NewNode("Dune"), NewNode("Frank Herbert"));
        graph.Apply(Added(dune));
        graph.Apply(Added(herbert));
        var link = NewLink(dune, herbert, type: "written-by");
        graph.Apply(Added(link));

        Assert.Same(link, graph.FindLink("written-by", dune.Id, herbert.Id));
        Assert.Null(graph.FindLink("written-by", herbert.Id, dune.Id));
    }

    [Fact]
    public void ALinkOfAnUnknownType_IsKept_AsDirected()
    {
        var (dune, herbert) = (NewNode("Dune"), NewNode("Frank Herbert"));
        Apply(dune, herbert);
        var link = NewLink(dune, herbert, type: "from-a-newer-version");

        Assert.True(_graph.Apply(Added(link)));
        Assert.Equal([link], _graph.LinksOf(dune.Id));
        Assert.Null(_graph.FindLink(link.Type, herbert.Id, dune.Id));
    }

    [Fact]
    public void ARepeatedLink_CountsOnce_EvenTypedTheOtherWay()
    {
        var (dune, herbert) = (NewNode("Dune"), NewNode("Frank Herbert"));
        Apply(dune, herbert);
        var first = NewLink(dune, herbert);
        _graph.Apply(Added(first));

        Assert.False(_graph.Apply(Added(NewLink(herbert, dune, minutes: 1))));
        Assert.Equal([first], _graph.LinksOf(dune.Id));
    }

    [Fact]
    public void ALinkBeforeItsNode_IsShownOnceTheNodeArrives()
    {
        var (dune, herbert) = (NewNode("Dune"), NewNode("Frank Herbert"));
        _graph.Apply(Added(dune));
        var link = NewLink(dune, herbert);
        _graph.Apply(Added(link));

        Assert.Empty(_graph.LinksOf(dune.Id));

        _graph.Apply(Added(herbert));
        Assert.Equal([link], _graph.LinksOf(dune.Id));
    }

    [Fact]
    public void ALinkToItself_IsListedOnce()
    {
        var dune = NewNode("Dune");
        _graph.Apply(Added(dune));

        _graph.Apply(Added(NewLink(dune, dune)));

        Assert.Single(_graph.LinksOf(dune.Id));
    }

    [Fact]
    public void Links_AreListedOldestFirst()
    {
        var (dune, herbert, arrakis) = (NewNode("Dune"), NewNode("Frank Herbert"), NewNode("Arrakis"));
        Apply(dune, herbert, arrakis);
        var newer = NewLink(dune, arrakis, minutes: 9);
        var older = NewLink(dune, herbert, minutes: 2);

        _graph.Apply(Added(newer));
        _graph.Apply(Added(older));

        Assert.Equal([older, newer], _graph.LinksOf(dune.Id));
    }

    [Fact]
    public void ANodeWithoutLinks_HasNone()
    {
        var dune = NewNode("Dune");
        _graph.Apply(Added(dune));

        Assert.Empty(_graph.LinksOf(dune.Id));
    }

    [Fact]
    public void AView_KeepsItsCurrentDefinition()
    {
        var view = Guid.NewGuid();
        var first = new ViewEdited(Guid.NewGuid(), Start, view, new FlatDefinition(TypeCatalog.Note.Id), []);
        var second = new ViewEdited(Guid.NewGuid(), Start.AddMinutes(1), view, new TreeDefinition(view, TypeCatalog.Contains.Id), [first.Id]);

        _graph.Apply(second);
        Assert.True(_graph.Apply(first));

        Assert.Equal([second], _graph.ViewVersionsOf(view));
    }

    [Fact]
    public void AnUnknownChange_IsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _graph.Apply(new OtherChange(Guid.NewGuid(), Start)));

    private void Apply(params Node[] nodes)
    {
        foreach (var node in nodes)
        {
            _graph.Apply(Added(node));
        }
    }

    private static Node NewNode(string name, int minutes = 0) =>
        new(Guid.NewGuid(), TypeCatalog.Generic.Id, name, Start.AddMinutes(minutes));

    private static Link NewLink(Node from, Node to, string? type = null, int minutes = 0) =>
        new(Guid.NewGuid(), type ?? TypeCatalog.RelatesTo.Id, from.Id, to.Id, Start.AddMinutes(minutes));

    private static NodeAdded Added(Node node) => new(Guid.NewGuid(), node.Created, node);

    private static LinkAdded Added(Link link) => new(Guid.NewGuid(), link.Created, link);

    private sealed record OtherChange(Guid Id, DateTimeOffset At) : Change(Id, At);
}
