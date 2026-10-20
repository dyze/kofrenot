namespace Kofrenot.Core.Tests.Input;

public sealed class RetypePlannerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();
    private readonly ManualClock _clock = new() { Now = Start.AddMinutes(10) };
    private readonly Node _dune = new(Guid.NewGuid(), TypeCatalog.Generic.Id, "Dune", Start);

    public RetypePlannerTests() => _graph.Add(_dune);

    private RetypePlan Plan(NodeType type) => new RetypePlanner(_graph, _clock).Plan(_graph.FindNode(_dune.Id)!, type);

    [Fact]
    public void AGenericNode_CanBecomeANoteADatasetOrAView()
    {
        Assert.Equal([TypeCatalog.Note, TypeCatalog.Dataset, TypeCatalog.View], RetypePlanner.TargetsOf(_dune));

        var plan = Plan(TypeCatalog.Note);

        Assert.Equal(RetypeProblem.None, plan.Problem);
        Assert.Equal((_dune.Id, "note", Start.AddMinutes(10)), (plan.Change!.Node, plan.Change.Type, plan.Change.At));
        Assert.Equal([_dune.Id], plan.Change.Replaces);
    }

    [Fact]
    public void AnotherNode_KeepsItsType()
    {
        _graph.Apply(Plan(TypeCatalog.Note).Change!);

        Assert.Empty(RetypePlanner.TargetsOf(_graph.FindNode(_dune.Id)!));
        Assert.Equal(RetypeProblem.NotGeneric, Plan(TypeCatalog.Dataset).Problem);
        Assert.Equal(RetypeProblem.Unchanged, Plan(TypeCatalog.Note).Problem);
        Assert.Null(Plan(TypeCatalog.Note).Change);
    }

    [Fact]
    public void AGenericNode_IsUnchanged_AsGeneric()
    {
        Assert.Equal(RetypeProblem.Unchanged, Plan(TypeCatalog.Generic).Problem);
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
        Assert.Equal(RetypeProblem.NotGeneric, Plan(TypeCatalog.View).Problem);
    }
}
