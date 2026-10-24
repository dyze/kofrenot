namespace Kofrenot.Core.Tests.Input;

public sealed class ColorPlannerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ManualClock _clock = new();
    private readonly KnowledgeGraph _graph = new();
    private readonly Node _dune = new(Guid.NewGuid(), TypeCatalog.Note.Id, "Dune", Start);
    private readonly ColorPlanner _planner;

    public ColorPlannerTests()
    {
        _graph.Add(_dune);
        _planner = new ColorPlanner(_graph, _clock);
    }

    [Fact]
    public void TheFirstColour_ReplacesNothing()
    {
        var change = _planner.Plan(_dune, NodeColor.Yellow)!;

        Assert.Equal((_dune.Id, "yellow", _clock.GetUtcNow()), (change.Node, change.Color, change.At));
        Assert.Empty(change.Replaces);
    }

    [Fact]
    public void NoColour_OnANodeWithout_ChangesNothing() =>
        Assert.Null(_planner.Plan(_dune, NodeColor.None));

    [Fact]
    public void TheSameColour_ChangesNothing()
    {
        _graph.Apply(_planner.Plan(_dune, NodeColor.Blue)!);

        Assert.Null(_planner.Plan(_dune, NodeColor.Blue));
    }

    [Fact]
    public void AnotherColour_OrNone_ReplacesTheCurrentOne()
    {
        var blue = _planner.Plan(_dune, NodeColor.Blue)!;
        _graph.Apply(blue);

        var none = _planner.Plan(_dune, NodeColor.None)!;

        Assert.Null(none.Color);
        Assert.Equal([blue.Id], none.Replaces);
    }

    [Fact]
    public void AnyColour_OfANodeInConflict_ReplacesEveryCurrentOne()
    {
        var mine = new NodeColored(Guid.NewGuid(), Start.AddMinutes(1), _dune.Id, "green", []);
        var theirs = new NodeColored(Guid.NewGuid(), Start.AddMinutes(2), _dune.Id, "blue", []);
        _graph.Apply(mine);
        _graph.Apply(theirs);

        var change = _planner.Plan(_dune, NodeColor.Blue)!;
        _graph.Apply(change);

        Assert.Equal([mine.Id, theirs.Id], change.Replaces);
        Assert.Equal(NodeColor.Blue, Assert.Single(_graph.ColorsOf(_dune.Id)).Color);
    }
}
