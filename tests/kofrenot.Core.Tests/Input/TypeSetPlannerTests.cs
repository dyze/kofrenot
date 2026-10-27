namespace Kofrenot.Core.Tests.Input;

public sealed class TypeSetPlannerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly NodeType Person = new("person", "Person");

    private readonly KnowledgeGraph _graph = new();
    private readonly ManualClock _clock = new() { Now = Start.AddMinutes(10) };
    private readonly Node _dune = new(Guid.NewGuid(), TypeCatalog.Generic.Id, "Dune", Start);

    public TypeSetPlannerTests()
    {
        _graph.Add(_dune);
        _graph.Apply(new NodeTypeDefined(Guid.NewGuid(), Start, Person.Id, Person.Name, null, null, [], []));
    }

    private Node Dune => _graph.FindNode(_dune.Id)!;

    private TypeSetPlanner Planner => new(_graph, _clock);

    private TypeSetPlan Add(NodeType type) => Planner.Add(Dune, type);

    private TypeSetPlan Remove(NodeType type) => Planner.Remove(Dune, type);

    private void Apply(TypeSetPlan plan)
    {
        foreach (var change in plan.Changes)
        {
            _graph.Apply(change);
        }
    }

    [Fact]
    public void ATypeAdded_TakesGenericOut()
    {
        var plan = Add(Person);

        Assert.Equal((TypeSetProblem.None, true, (string?)null), (plan.Problem, plan.IsAdded, plan.Hidden));
        var removal = Assert.IsType<NodeTypeRemoved>(plan.Changes[0]);
        Assert.Equal([_dune.Id], removal.Removes);
        var added = Assert.IsType<NodeTypeAdded>(plan.Changes[1]);
        Assert.Equal((_dune.Id, "person", Start.AddMinutes(10)), (added.Node, added.Type, added.At));

        Apply(plan);

        Assert.Equal(["person"], _graph.TypeIdsOf(_dune.Id));
    }

    [Fact]
    public void ASecondType_IsAdded_BesideTheFirst()
    {
        Apply(Add(Person));
        _clock.Now = Start.AddMinutes(11);

        var plan = Add(TypeCatalog.Note);

        Assert.IsType<NodeTypeAdded>(Assert.Single(plan.Changes));
        Apply(plan);
        Assert.Equal(["person", "note"], _graph.TypeIdsOf(_dune.Id));
    }

    [Fact]
    public void ATypeWithContent_ReplacesTheOtherOne_HidingItsContent()
    {
        Apply(Add(TypeCatalog.Note));
        _graph.Apply(new NoteEdited(Guid.NewGuid(), Start, _dune.Id, []));

        var plan = Add(TypeCatalog.Dataset);

        Assert.Equal("Its text is hidden; making it a note again shows it.", plan.Hidden);
        Apply(plan);
        Assert.Equal(["dataset"], _graph.TypeIdsOf(_dune.Id));

        Apply(Add(TypeCatalog.Note));
        Assert.Single(_graph.NoteVersionsOf(_dune.Id));
        Assert.Equal("note", Dune.Type);
    }

    [Fact]
    public void ADatasetsFile_IsHidden_ByItsLatestName()
    {
        Apply(Add(TypeCatalog.Dataset));
        var first = new DatasetEdited(Guid.NewGuid(), Start, _dune.Id, new DatasetFile("cover.webp", 10, new string('a', 64)), []);
        _graph.Apply(first);
        _graph.Apply(new DatasetEdited(Guid.NewGuid(), Start.AddMinutes(1), _dune.Id, new DatasetFile("cover-2.webp", 10, new string('b', 64)), [first.Id]));

        Assert.Equal("Its file cover-2.webp is hidden; making it a dataset again shows it.", Add(TypeCatalog.Note).Hidden);
        Assert.Equal("Its file cover-2.webp is hidden; making it a dataset again shows it.", Remove(TypeCatalog.Dataset).Hidden);
        Assert.Null(Add(Person).Hidden);
    }

    [Fact]
    public void AViewsDefinition_IsHidden_AndItsTabCloses()
    {
        Apply(Add(TypeCatalog.View));
        _graph.Apply(new ViewEdited(Guid.NewGuid(), Start, _dune.Id, new FlatDefinition(TypeCatalog.Note.Id), []));

        Assert.Equal("What it shows is hidden, and its tab closes; making it a view again shows it.", Remove(TypeCatalog.View).Hidden);
    }

    [Fact]
    public void ANodeWithNothingToShow_HidesNothing()
    {
        Apply(Add(TypeCatalog.Note));

        Assert.Null(Add(TypeCatalog.Dataset).Hidden);
        Assert.Null(Remove(TypeCatalog.Note).Hidden);
    }

    [Fact]
    public void ATypeItHas_OrGeneric_IsUnchanged()
    {
        Apply(Add(Person));

        Assert.Equal(TypeSetProblem.Unchanged, Add(Person).Problem);
        Assert.Empty(Add(Person).Changes);
        Assert.Equal(TypeSetProblem.Unchanged, Add(TypeCatalog.Generic).Problem);
        Assert.Equal(TypeSetProblem.Unchanged, Remove(TypeCatalog.Note).Problem);
        Assert.Equal(TypeSetProblem.Unchanged, Remove(TypeCatalog.Generic).Problem);
    }

    [Fact]
    public void ATypeRemoved_IsTakenOut_ByEveryChangeThatGaveIt()
    {
        var mine = new NodeTypeAdded(Guid.NewGuid(), Start.AddMinutes(1), _dune.Id, "person");
        var theirs = new NodeTypeAdded(Guid.NewGuid(), Start.AddMinutes(2), _dune.Id, "person");
        _graph.Apply(mine);
        _graph.Apply(theirs);

        var plan = Remove(Person);

        Assert.Equal((TypeSetProblem.None, false), (plan.Problem, plan.IsAdded));
        Assert.Equal([mine.Id, theirs.Id], Assert.IsType<NodeTypeRemoved>(Assert.Single(plan.Changes)).Removes);
        Apply(plan);
        Assert.Equal(["generic"], _graph.TypeIdsOf(_dune.Id));
    }

    [Fact]
    public void RemovingItsLastType_MakesItGeneric()
    {
        Apply(Add(Person));
        Apply(Remove(Person));

        Assert.Empty(_graph.TypesOf(_dune.Id));
        Assert.Equal("generic", Dune.Type);
        Assert.True(_graph.HasType(_dune.Id, "generic"));
        Assert.False(_graph.HasType(Guid.NewGuid(), "generic"));
    }
}
