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
}
