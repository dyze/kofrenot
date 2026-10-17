namespace Kofrenot.Core.Tests.Input;

public sealed class AliasPlannerTests
{
    private readonly ManualClock _clock = new();
    private readonly KnowledgeGraph _graph = new();
    private readonly AliasPlanner _planner;

    public AliasPlannerTests() => _planner = new AliasPlanner(_graph, _clock);

    [Fact]
    public void ANewPhrase_AddsAnAlias_OfTheWayChosen()
    {
        var plan = _planner.Plan(TypeCatalog.Contains.Reverse!, "  is   in ");

        Assert.Equal(AliasProblem.None, plan.Problem);
        Assert.True(plan.CanAdd);
        Assert.Equal("is in", plan.Phrase);
        Assert.Equal("Adds “is in” to “contained by”.", plan.Status);
        var change = plan.Change!;
        Assert.Equal(("contains", true, "is in", _clock.Now), (change.LinkType, change.IsReverse, change.Phrase, change.At));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NoPhrase_CannotBeAdded(string text)
    {
        var plan = _planner.Plan(TypeCatalog.RelatesTo.Forward, text);

        Assert.Equal(AliasProblem.Empty, plan.Problem);
        Assert.False(plan.CanAdd);
        Assert.Equal("Type a phrase.", plan.Status);
    }

    [Theory]
    [InlineData("Contained  BY", "contained by")]
    [InlineData("related to", "relates to")]
    public void APhraseInUse_CannotBeAdded(string text, string takenBy)
    {
        var plan = _planner.Plan(TypeCatalog.IsA.Forward, text);

        Assert.Equal(AliasProblem.Taken, plan.Problem);
        Assert.Equal(takenBy, plan.TakenBy!.Name);
        Assert.Null(plan.Change);
        Assert.Equal($"“{plan.Phrase}” is a phrase of “{takenBy}” already.", plan.Status);
    }

    [Fact]
    public void AnAliasInUse_CannotBeAddedAgain()
    {
        _graph.Apply(_planner.Plan(TypeCatalog.RelatesTo.Forward, "-").Change!);

        Assert.Equal(AliasProblem.Taken, _planner.Plan(TypeCatalog.Contains.Forward, "-").Problem);
    }

    [Fact]
    public void NodeNamesContainingThePhrase_AreWarnedAbout()
    {
        Add("Tom and Jerry");
        Add("Andrew");
        Add("and so on");

        var one = _planner.Plan(TypeCatalog.RelatesTo.Forward, "and");
        Add("Salt and pepper");
        var two = _planner.Plan(TypeCatalog.RelatesTo.Forward, "and");

        Assert.Equal(["Tom and Jerry"], one.Hidden.Select(node => node.Name));
        Assert.True(one.CanAdd);
        Assert.Equal("Adds “and” to “relates to”. 1 node name contains it: the input box could no longer find it.", one.Status);
        Assert.Equal("Adds “and” to “relates to”. 2 node names contain it: the input box could no longer find them.", two.Status);
    }

    [Fact]
    public void Removing_TakesEveryCopyOfTheAlias()
    {
        var first = new AliasAdded(Guid.NewGuid(), _clock.Now, "relates-to", false, "-");
        var copy = new AliasAdded(Guid.NewGuid(), _clock.Now.AddMinutes(1), "relates-to", false, "-");
        var other = new AliasAdded(Guid.NewGuid(), _clock.Now, "relates-to", false, "with");
        foreach (var alias in new[] { first, copy, other })
        {
            _graph.Apply(alias);
        }

        var removal = _planner.Remove(_graph.Aliases.All.Single(alias => alias.Id == first.Id));
        _graph.Apply(removal);

        Assert.Equal(new[] { first.Id, copy.Id }.Order(), removal.Removes.Order());
        Assert.Equal(_clock.Now, removal.At);
        Assert.Equal([other.Id], _graph.Aliases.All.Select(alias => alias.Id));
    }

    private void Add(string name) => _graph.Add(new Node(Guid.NewGuid(), "generic", name, _clock.Now));
}
