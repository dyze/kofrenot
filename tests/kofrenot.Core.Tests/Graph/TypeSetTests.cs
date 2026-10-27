namespace Kofrenot.Core.Tests.Graph;

public sealed class TypeSetTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();
    private readonly Node _frank = new(Guid.NewGuid(), TypeCatalog.Generic.Id, "Frank Herbert", Start);

    public TypeSetTests() => _graph.Add(_frank);

    private NodeTypeAdded Add(string type, int minutes) => new(Guid.NewGuid(), Start.AddMinutes(minutes), _frank.Id, type);

    private NodeTypeRemoved Remove(int minutes, params Guid[] removes) => new(Guid.NewGuid(), Start.AddMinutes(minutes), _frank.Id, removes);

    [Fact]
    public void ATypeAdded_JoinsTheTypes_AndIsAppliedOnce()
    {
        var person = Add("person", 1);

        Assert.True(_graph.Apply(person));
        Assert.False(_graph.Apply(person));

        Assert.Equal([new TypeVersion(_frank.Id, "generic", Start), new TypeVersion(person.Id, "person", person.At)], _graph.TypesOf(_frank.Id));
        Assert.Equal(["generic", "person"], _graph.TypeIdsOf(_frank.Id));
        Assert.True(_graph.HasType(_frank.Id, "person"));
        Assert.False(_graph.HasType(_frank.Id, "author"));
        Assert.Equal("person", _graph.FindNode(_frank.Id)!.Type);
        Assert.Equal([person], _graph.TypeChangesOf(_frank.Id));
    }

    [Fact]
    public void TypesAddedOnTwoDevices_AreBothKept()
    {
        _graph.Apply(Add("author", 2));
        _graph.Apply(Add("person", 1));

        Assert.Equal(["generic", "person", "author"], _graph.TypeIdsOf(_frank.Id));
    }

    [Fact]
    public void TheSameTypeAddedTwice_IsOneType_GivenByBoth()
    {
        var (mine, theirs) = (Add("person", 1), Add("person", 2));
        _graph.Apply(mine);
        _graph.Apply(theirs);

        Assert.Equal(["generic", "person"], _graph.TypeIdsOf(_frank.Id));
        Assert.Equal([mine.Id, theirs.Id], _graph.TypesOf(_frank.Id).Where(type => type.Type == "person").Select(type => type.Id));
    }

    [Fact]
    public void ATypeRemoved_LeavesTheTypes_ByTheChangesThatGaveIt()
    {
        var person = Add("person", 1);
        _graph.Apply(person);

        Assert.True(_graph.Apply(Remove(2, _frank.Id)));

        Assert.Equal(["person"], _graph.TypeIdsOf(_frank.Id));

        _graph.Apply(Remove(3, person.Id));

        Assert.Empty(_graph.TypesOf(_frank.Id));
        Assert.Equal("generic", _graph.FindNode(_frank.Id)!.Type);
    }

    [Fact]
    public void ARemovalArrivingFirst_StillRemoves_ButNotATypeAddedAgainLater()
    {
        var person = Add("person", 1);
        _graph.Apply(Remove(2, person.Id));
        _graph.Apply(person);

        Assert.False(_graph.HasType(_frank.Id, "person"));

        _graph.Apply(Add("person", 3));

        Assert.True(_graph.HasType(_frank.Id, "person"));
    }

    [Fact]
    public void ATypeWithContent_IsTheTypeShown_WhicheverIsLatest()
    {
        _graph.Apply(Add("note", 1));
        _graph.Apply(Add("person", 2));

        Assert.Equal("note", _graph.FindNode(_frank.Id)!.Type);
        Assert.Equal("note", Assert.Single(_graph.NodesNamed("frank herbert")).Type);
    }

    [Fact]
    public void ARetypeBeforeFormat8_IsTheTypeItStartsFrom()
    {
        var retype = new NodeRetyped(Guid.NewGuid(), Start.AddMinutes(1), _frank.Id, "note", [_frank.Id]);
        _graph.Apply(retype);
        _graph.Apply(Add("person", 2));

        Assert.Equal(["note", "person"], _graph.TypeIdsOf(_frank.Id));

        _graph.Apply(Remove(3, retype.Id));

        Assert.Equal(["person"], _graph.TypeIdsOf(_frank.Id));
    }

    [Fact]
    public void TypesOfANodeNotArrived_ShowWhenItArrives()
    {
        var graph = new KnowledgeGraph();
        var dune = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Dune", Start);

        Assert.True(graph.Apply(new NodeTypeAdded(Guid.NewGuid(), Start.AddMinutes(1), dune.Id, "note")));
        Assert.Empty(graph.TypesOf(dune.Id));
        graph.Add(dune);

        Assert.Equal("note", graph.FindNode(dune.Id)!.Type);
    }
}
