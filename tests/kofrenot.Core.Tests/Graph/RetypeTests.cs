namespace Kofrenot.Core.Tests.Graph;

public sealed class RetypeTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();
    private readonly Node _dune = new(Guid.NewGuid(), TypeCatalog.Generic.Id, "Dune", Start);

    public RetypeTests() => _graph.Add(_dune);

    private NodeRetyped RetypeDune(string type, int minutes, params Guid[] replaces) =>
        new(Guid.NewGuid(), Start.AddMinutes(minutes), _dune.Id, type, replaces);

    [Fact]
    public void ANode_HasTheTypeItWasAddedWith()
    {
        Assert.Equal([new TypeVersion(_dune.Id, "generic", Start)], _graph.TypesOf(_dune.Id));
        Assert.Empty(_graph.TypesOf(Guid.NewGuid()));
    }

    [Fact]
    public void ARetype_ChangesTheTypeShown_KeepingTheNameSearched()
    {
        var retype = RetypeDune("note", 1, _dune.Id);

        Assert.True(_graph.Apply(retype));
        Assert.False(_graph.Apply(retype));

        Assert.Equal("note", _graph.FindNode(_dune.Id)!.Type);
        Assert.Equal("note", Assert.Single(_graph.NodesNamed("dune")).Type);
        Assert.Equal([new TypeVersion(retype.Id, "note", retype.At)], _graph.TypesOf(_dune.Id));
        Assert.Equal([retype], _graph.TypeChangesOf(_dune.Id));
    }

    [Fact]
    public void ARetypeAfterARename_KeepsTheNewName_AndTheOtherWayRound()
    {
        _graph.Apply(new NodeRenamed(Guid.NewGuid(), Start.AddMinutes(1), _dune.Id, "Dune (novel)", [_dune.Id]));
        _graph.Apply(RetypeDune("note", 2, _dune.Id));

        Assert.Equal(("Dune (novel)", "note"), (_graph.FindNode(_dune.Id)!.Name, _graph.FindNode(_dune.Id)!.Type));

        _graph.Apply(new NodeRenamed(Guid.NewGuid(), Start.AddMinutes(3), _dune.Id, "Dune, 1965", [.. _graph.NamesOf(_dune.Id).Select(name => name.Id)]));

        Assert.Equal(("Dune, 1965", "note"), (_graph.FindNode(_dune.Id)!.Name, _graph.FindNode(_dune.Id)!.Type));
    }

    [Fact]
    public void RetypesMadeApart_AreBothKept_ShowingTheLatest()
    {
        var mine = RetypeDune("note", 1, _dune.Id);
        var theirs = RetypeDune("dataset", 2, _dune.Id);

        _graph.Apply(theirs);
        _graph.Apply(mine);

        Assert.Equal(["note", "dataset"], _graph.TypesOf(_dune.Id).Select(version => version.Type));
        Assert.Equal("dataset", _graph.FindNode(_dune.Id)!.Type);
    }

    [Fact]
    public void ARetypeReplacingBoth_ResolvesTheConflict()
    {
        var mine = RetypeDune("note", 1, _dune.Id);
        var theirs = RetypeDune("dataset", 2, _dune.Id);
        _graph.Apply(mine);
        _graph.Apply(theirs);

        _graph.Apply(RetypeDune("note", 3, mine.Id, theirs.Id));

        Assert.Equal("note", Assert.Single(_graph.TypesOf(_dune.Id)).Type);
        Assert.Equal("note", _graph.FindNode(_dune.Id)!.Type);
    }

    [Fact]
    public void ARetypeArrivingBeforeItsNode_IsAppliedWhenTheNodeArrives()
    {
        var graph = new KnowledgeGraph();
        var arrakis = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Arrakis", Start);

        Assert.True(graph.Apply(new NodeRetyped(Guid.NewGuid(), Start.AddMinutes(1), arrakis.Id, "note", [arrakis.Id])));
        Assert.Null(graph.FindNode(arrakis.Id));
        graph.Add(arrakis);

        Assert.Equal("note", graph.FindNode(arrakis.Id)!.Type);
    }

    [Fact]
    public void ARetypeOfARemovedNode_ShowsNothing()
    {
        _graph.Apply(new ChangesRemoved(Guid.NewGuid(), Start.AddMinutes(1), [_dune.Id]));

        Assert.True(_graph.Apply(RetypeDune("note", 2, _dune.Id)));

        Assert.Null(_graph.FindNode(_dune.Id));
        Assert.Empty(_graph.NodesNamed("Dune"));
    }
}
