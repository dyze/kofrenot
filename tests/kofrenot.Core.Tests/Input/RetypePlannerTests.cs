namespace Kofrenot.Core.Tests.Input;

public sealed class RetypePlannerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();
    private readonly ManualClock _clock = new() { Now = Start.AddMinutes(10) };
    private readonly Node _dune = new(Guid.NewGuid(), TypeCatalog.Generic.Id, "Dune", Start);

    public RetypePlannerTests() => _graph.Add(_dune);

    private Node Dune => _graph.FindNode(_dune.Id)!;

    private RetypePlan Plan(NodeType type) => new RetypePlanner(_graph, _clock).Plan(Dune, type);

    [Fact]
    public void AGenericNode_CanBecomeANoteADatasetOrAView()
    {
        var plan = Plan(TypeCatalog.Note);

        Assert.Equal(RetypeProblem.None, plan.Problem);
        Assert.Equal((_dune.Id, "note", Start.AddMinutes(10)), (plan.Change!.Node, plan.Change.Type, plan.Change.At));
        Assert.Equal([_dune.Id], plan.Change.Replaces);
        Assert.Null(plan.Hidden);
    }

    [Fact]
    public void AnyNode_CanTakeAnyOtherType()
    {
        _graph.Apply(Plan(TypeCatalog.Dataset).Change!);

        Assert.Equal(RetypeProblem.None, Plan(TypeCatalog.Generic).Problem);
        Assert.Equal(RetypeProblem.None, Plan(TypeCatalog.Note).Problem);
        Assert.Equal(RetypeProblem.None, Plan(TypeCatalog.View).Problem);
        Assert.Equal(RetypeProblem.Unchanged, Plan(TypeCatalog.Dataset).Problem);
        Assert.Null(Plan(TypeCatalog.Dataset).Change);
    }

    [Fact]
    public void AGenericNode_IsUnchanged_AsGeneric() =>
        Assert.Equal(RetypeProblem.Unchanged, Plan(TypeCatalog.Generic).Problem);

    [Fact]
    public void ANotesText_IsHidden_AndShownAgainAsANote()
    {
        _graph.Apply(Plan(TypeCatalog.Note).Change!);
        _graph.Apply(new NoteEdited(Guid.NewGuid(), Start, _dune.Id, []));

        Assert.Equal("Its text is hidden; making it a note again shows it.", Plan(TypeCatalog.Generic).Hidden);

        _graph.Apply(Plan(TypeCatalog.Generic).Change!);
        _graph.Apply(Plan(TypeCatalog.Note).Change!);
        Assert.Single(_graph.NoteVersionsOf(_dune.Id));
    }

    [Fact]
    public void ADatasetsFile_IsHidden_ByItsLatestName()
    {
        _graph.Apply(Plan(TypeCatalog.Dataset).Change!);
        var first = new DatasetEdited(Guid.NewGuid(), Start, _dune.Id, new DatasetFile("cover.webp", 10, new string('a', 64)), []);
        _graph.Apply(first);
        _graph.Apply(new DatasetEdited(Guid.NewGuid(), Start.AddMinutes(1), _dune.Id, new DatasetFile("cover-2.webp", 10, new string('b', 64)), [first.Id]));

        Assert.Equal("Its file cover-2.webp is hidden; making it a dataset again shows it.", Plan(TypeCatalog.Note).Hidden);
    }

    [Fact]
    public void AViewsDefinition_IsHidden_AndItsTabCloses()
    {
        _graph.Apply(Plan(TypeCatalog.View).Change!);
        _graph.Apply(new ViewEdited(Guid.NewGuid(), Start, _dune.Id, new FlatDefinition(TypeCatalog.Note.Id), []));

        Assert.Equal("What it shows is hidden, and its tab closes; making it a view again shows it.", Plan(TypeCatalog.Generic).Hidden);
    }

    [Fact]
    public void ANodeWithNothingToShow_HidesNothing()
    {
        _graph.Apply(Plan(TypeCatalog.Note).Change!);

        Assert.Null(Plan(TypeCatalog.Dataset).Hidden);
    }

    [Fact]
    public void ChoosingOneTypeOfAConflict_ReplacesBoth()
    {
        var mine = new NodeRetyped(Guid.NewGuid(), Start.AddMinutes(1), _dune.Id, "note", [_dune.Id]);
        var theirs = new NodeRetyped(Guid.NewGuid(), Start.AddMinutes(2), _dune.Id, "dataset", [_dune.Id]);
        _graph.Apply(mine);
        _graph.Apply(theirs);

        var plan = Plan(TypeCatalog.Note);

        Assert.Equal(RetypeProblem.None, plan.Problem);
        Assert.Equal([mine.Id, theirs.Id], plan.Change!.Replaces);
        Assert.Equal(RetypeProblem.None, Plan(TypeCatalog.Dataset).Problem);
        Assert.Null(Plan(TypeCatalog.Dataset).Hidden);
    }
}
