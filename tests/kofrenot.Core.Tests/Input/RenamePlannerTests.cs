namespace Kofrenot.Core.Tests.Input;

public sealed class RenamePlannerTests
{
    private readonly ManualClock _clock = new();
    private readonly KnowledgeGraph _graph = new();
    private readonly RenamePlanner _planner;
    private readonly Node _dune;

    public RenamePlannerTests()
    {
        _planner = new RenamePlanner(_graph, clock: _clock);
        _dune = Add("Dune");
    }

    [Fact]
    public void ANewName_RenamesTheNode_ReplacingItsName()
    {
        var plan = _planner.Plan(_dune, "  Dune   (novel) ");

        Assert.Equal(RenameProblem.None, plan.Problem);
        Assert.True(plan.CanRename);
        Assert.Equal("Dune (novel)", plan.Name);
        Assert.Empty(plan.Homonyms);
        var change = plan.Change!;
        Assert.Equal((_dune.Id, "Dune (novel)", _clock.Now), (change.Node, change.Name, change.At));
        Assert.Equal([_dune.Id], change.Replaces);
    }

    [Theory]
    [InlineData("", RenameProblem.Empty)]
    [InlineData("   ", RenameProblem.Empty)]
    [InlineData("Dune", RenameProblem.Unchanged)]
    [InlineData("Dune relates to Arrakis", RenameProblem.LinkPhrase)]
    public void SomeNames_CannotBeGiven(string text, RenameProblem problem)
    {
        var plan = _planner.Plan(_dune, text);

        Assert.Equal(problem, plan.Problem);
        Assert.False(plan.CanRename);
    }

    [Theory]
    [InlineData("", "Type a name.")]
    [InlineData("Dune relates to Arrakis", "A name cannot contain a link phrase, such as “relates to”.")]
    [InlineData("Dune", "Same name. Esc to cancel.")]
    [InlineData("Dune (novel)", "Enter renames, Esc cancels.")]
    [InlineData("arrakis", "“arrakis” exists already. Enter renames anyway, Esc cancels.")]
    [InlineData("Caladan", "“Caladan” exists already (2 times). Enter renames anyway, Esc cancels.")]
    public void TheStatus_SaysWhatEnterDoes(string text, string status)
    {
        Add("Arrakis");
        Add("Caladan");
        Add("Caladan");

        Assert.Equal(status, _planner.Plan(_dune, text).Status);
    }

    [Fact]
    public void AChangeOfCaseOnly_IsARename() =>
        Assert.True(_planner.Plan(_dune, "DUNE").CanRename);

    [Fact]
    public void ANameOtherNodesHave_IsAllowed_AndTheyAreListed()
    {
        var other = Add("Arrakis");

        var plan = _planner.Plan(_dune, "arrakis");

        Assert.True(plan.CanRename);
        Assert.Equal([other], plan.Homonyms);
    }

    [Fact]
    public void InAConflict_KeepingOneOfTheNames_ReplacesThemAll()
    {
        var mine = new NodeRenamed(Guid.NewGuid(), _clock.Now.AddMinutes(1), _dune.Id, "Dune (novel)", [_dune.Id]);
        var theirs = new NodeRenamed(Guid.NewGuid(), _clock.Now.AddMinutes(2), _dune.Id, "Dune, 1965", [_dune.Id]);
        _graph.Apply(mine);
        _graph.Apply(theirs);

        var plan = _planner.Plan(_graph.FindNode(_dune.Id)!, "Dune, 1965");

        Assert.True(plan.CanRename);
        Assert.Equal([mine.Id, theirs.Id], plan.Change!.Replaces);
    }

    private Node Add(string name)
    {
        var node = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, name, _clock.Now);
        _graph.Add(node);
        return node;
    }
}
