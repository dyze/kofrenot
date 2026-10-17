namespace Kofrenot.Core.Tests.Graph;

public sealed class RenameTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();
    private readonly Node _dune = new(Guid.NewGuid(), TypeCatalog.Generic.Id, "Dune", Start);

    public RenameTests() => _graph.Add(_dune);

    [Fact]
    public void ARename_ChangesTheNameShown_AndTheNameSearched()
    {
        var rename = Rename("Dune (novel)", minutes: 1, _dune.Id);

        Assert.True(_graph.Apply(rename));

        Assert.Equal("Dune (novel)", _graph.FindNode(_dune.Id)!.Name);
        Assert.Empty(_graph.NodesNamed("Dune"));
        Assert.Equal([_dune.Id], _graph.NodesNamed("dune (NOVEL)").Select(node => node.Id));
        Assert.Equal([new NameVersion(rename.Id, "Dune (novel)", rename.At)], _graph.NamesOf(_dune.Id));
    }

    [Fact]
    public void ARenameAppliedTwice_ChangesNothing()
    {
        var rename = Rename("Arrakis", minutes: 1, _dune.Id);
        _graph.Apply(rename);

        Assert.False(_graph.Apply(rename));
        Assert.Single(_graph.NamesOf(_dune.Id));
    }

    [Fact]
    public void RenamesInARow_ShowTheLast()
    {
        var first = Rename("Dune (novel)", minutes: 1, _dune.Id);
        var second = Rename("Dune, 1965", minutes: 2, first.Id);

        // The sync tool may copy them in any order.
        _graph.Apply(second);
        _graph.Apply(first);

        Assert.Equal("Dune, 1965", _graph.FindNode(_dune.Id)!.Name);
        Assert.Single(_graph.NamesOf(_dune.Id));
    }

    [Fact]
    public void RenamesMadeApart_AreAConflict_ShowingTheLatest()
    {
        var mine = Rename("Dune (novel)", minutes: 1, _dune.Id);
        var theirs = Rename("Dune, 1965", minutes: 2, _dune.Id);

        _graph.Apply(theirs);
        _graph.Apply(mine);

        Assert.Equal(["Dune (novel)", "Dune, 1965"], _graph.NamesOf(_dune.Id).Select(version => version.Name));
        Assert.Equal("Dune, 1965", _graph.FindNode(_dune.Id)!.Name);
    }

    [Fact]
    public void ARenameReplacingBothNames_ResolvesTheConflict()
    {
        var mine = Rename("Dune (novel)", minutes: 1, _dune.Id);
        var theirs = Rename("Dune, 1965", minutes: 2, _dune.Id);
        _graph.Apply(mine);
        _graph.Apply(theirs);

        _graph.Apply(Rename("Dune (novel)", minutes: 3, mine.Id, theirs.Id));

        Assert.Single(_graph.NamesOf(_dune.Id));
        Assert.Equal("Dune (novel)", _graph.FindNode(_dune.Id)!.Name);
    }

    [Fact]
    public void ARenameArrivingBeforeItsNode_IsAppliedWhenTheNodeArrives()
    {
        var graph = new KnowledgeGraph();
        var arrakis = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Arakis", Start);
        graph.Apply(new NodeRenamed(Guid.NewGuid(), Start.AddMinutes(1), arrakis.Id, "Arrakis", [arrakis.Id]));

        Assert.Empty(graph.NamesOf(arrakis.Id));
        graph.Add(arrakis);

        Assert.Equal("Arrakis", graph.FindNode(arrakis.Id)!.Name);
    }

    [Fact]
    public void ARenameOfAnUnknownNode_IsKept_ButShowsNothing()
    {
        var rename = new NodeRenamed(Guid.NewGuid(), Start, Guid.NewGuid(), "Ghost", [Guid.NewGuid()]);

        Assert.True(_graph.Apply(rename));
        Assert.False(_graph.Apply(rename));
        Assert.Single(_graph.Nodes);
        Assert.Empty(_graph.NamesOf(rename.Node));
    }

    [Fact]
    public void ANameNoRenameLeftCurrent_FallsBackToTheNameAsAdded()
    {
        // Damaged history: a rename replacing itself.
        var id = Guid.NewGuid();
        _graph.Apply(new NodeRenamed(id, Start.AddMinutes(1), _dune.Id, "Lost", [_dune.Id, id]));

        Assert.Equal("Dune", _graph.FindNode(_dune.Id)!.Name);
    }

    [Fact]
    public void AnUnknownNode_HasNoNames() =>
        Assert.Empty(_graph.NamesOf(Guid.NewGuid()));

    private NodeRenamed Rename(string name, int minutes, params Guid[] replaces) =>
        new(Guid.NewGuid(), Start.AddMinutes(minutes), _dune.Id, name, replaces);
}
