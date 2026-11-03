namespace Kofrenot.Core.Tests.Views;

public sealed class ViewPlannerTests
{
    private readonly TreeGraph _tree = new();
    private readonly ManualClock _clock = new();
    private readonly ViewPlanner _planner;

    public ViewPlannerTests() => _planner = new ViewPlanner(_tree.Graph, _clock);

    [Fact]
    public void AViewWithoutVersion_HasNoDefinition()
    {
        var state = _planner.Load(_tree.Add("Files", TypeCatalog.View.Id));

        Assert.Empty(state.Versions);
        Assert.Null(state.Definition);
        Assert.False(state.IsConflict);
    }

    [Fact]
    public void AConflict_ShowsTheLatestDefinition_AndSavingReplacesBoth()
    {
        var files = _tree.Add("Files", TypeCatalog.View.Id);
        var first = new ViewEdited(Guid.NewGuid(), TreeGraph.Start, files.Id, new FlatDefinition(TypeCatalog.Note.Id), []);
        var conflict = new ViewEdited(Guid.NewGuid(), TreeGraph.Start.AddMinutes(1), files.Id, new FlatDefinition(TypeCatalog.Dataset.Id), []);
        _tree.Apply([conflict, first]);

        var state = _planner.Load(files);
        var saved = _planner.Save(files, conflict.Definition, state)!;

        Assert.True(state.IsConflict);
        Assert.Equal(conflict.Definition, state.Definition);
        Assert.Equal([first.Id, conflict.Id], saved.Replaces);
        Assert.Equal((files.Id, _clock.Now), (saved.Node, saved.At));
    }

    [Fact]
    public void TheSameDefinition_WithoutConflict_SavesNothing()
    {
        var files = _tree.Add("Files", TypeCatalog.View.Id);
        _tree.Apply([new ViewEdited(Guid.NewGuid(), TreeGraph.Start, files.Id, new FlatDefinition(TypeCatalog.Note.Id), [])]);
        var state = _planner.Load(files);

        Assert.Null(_planner.Save(files, new FlatDefinition(TypeCatalog.Note.Id), state));
        Assert.NotNull(_planner.Save(files, new FlatDefinition(TypeCatalog.Dataset.Id), state));
    }

    [Fact]
    public void TheSameGraph_InOtherLists_SavesNothing()
    {
        var family = _tree.Add("Family", TypeCatalog.View.Id);
        var root = Guid.NewGuid();
        GraphDefinition Graph(int depth) => new(root, depth, GraphLayoutKind.Layered, [new("parent", GraphRole.Down)], ["person"], ["Born"]);
        _tree.Apply([new ViewEdited(Guid.NewGuid(), TreeGraph.Start, family.Id, Graph(2), [])]);
        var state = _planner.Load(family);

        Assert.Null(_planner.Save(family, Graph(2), state));
        Assert.Equal(Graph(2).GetHashCode(), state.Definition!.GetHashCode());
        Assert.NotNull(_planner.Save(family, Graph(3), state));
        Assert.NotNull(_planner.Save(family, Graph(2) with { Fields = ["Died"] }, state));
        Assert.False(Graph(2).Equals(null));
    }
}
