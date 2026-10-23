namespace Kofrenot.Core.Tests.Input;

public sealed class LinkTypePlannerTests
{
    private readonly ManualClock _clock = new();
    private readonly KnowledgeGraph _graph = new();
    private readonly LinkTypePlanner _planner;

    public LinkTypePlannerTests() => _planner = new LinkTypePlanner(_graph, _clock);

    [Fact]
    public void ANewName_AddsALinkType_ReadTheSameBothWays()
    {
        var plan = _planner.Plan("  knows  ", "  ");

        Assert.Equal(LinkTypeProblem.None, plan.Problem);
        Assert.True(plan.CanApply);
        Assert.Equal("Adds “knows”, read the same both ways.", plan.Status);
        var change = plan.Change!;
        Assert.Equal((change.Id.ToString(), "knows", (string?)null, _clock.Now), (change.LinkType, change.Name, change.ReverseName, change.At));
        Assert.Empty(change.Replaces);
    }

    [Fact]
    public void AReverseName_MakesItReadEachWay()
    {
        var plan = _planner.Plan("wrote", " written   by ");

        Assert.Equal("Adds “wrote”, read “written by” the other way.", plan.Status);
        Assert.Equal("written by", plan.Change!.ReverseName);
    }

    [Fact]
    public void NoName_CannotBeAdded()
    {
        var plan = _planner.Plan("  ", "written by");

        Assert.Equal(LinkTypeProblem.Empty, plan.Problem);
        Assert.False(plan.CanApply);
        Assert.Equal("Type a name.", plan.Status);
    }

    [Fact]
    public void TheSameNameBothWays_CannotBeAdded()
    {
        var plan = _planner.Plan("knows", "Knows");

        Assert.Equal(LinkTypeProblem.SameBothWays, plan.Problem);
        Assert.Equal("Type another name for the other way, or none for a link that reads the same both ways.", plan.Status);
    }

    [Theory]
    [InlineData("contains", null, "contains", "contains")]
    [InlineData("holds", "Contained  by", "Contained by", "contained by")]
    [InlineData("-", null, "-", "relates to")]
    [InlineData("knows", null, "knows", "knows")]
    public void APhraseInUse_CannotBeAdded(string name, string? reverse, string taken, string takenBy)
    {
        _graph.Apply(new AliasAdded(Guid.NewGuid(), _clock.Now, "relates-to", false, "-"));
        _graph.Apply(_planner.Plan("knows", null).Change!);

        var plan = _planner.Plan(name, reverse);

        Assert.Equal(LinkTypeProblem.Taken, plan.Problem);
        Assert.Equal((taken, takenBy), (plan.Taken, plan.TakenBy!.Name));
        Assert.Equal($"“{taken}” is a phrase of “{takenBy}” already.", plan.Status);
    }

    [Fact]
    public void NodeNamesContainingAName_AreWarnedAbout()
    {
        Add("Tom knows Jerry");
        Add("Knowsley");
        var one = _planner.Plan("knows", null);
        Add("Who wrote it");
        var two = _planner.Plan("knows", "wrote");

        Assert.Equal(["Tom knows Jerry"], one.Hidden.Select(node => node.Name));
        Assert.True(one.CanApply);
        Assert.EndsWith(" 1 node name contains it: the input box could no longer find it.", one.Status);
        Assert.EndsWith(" 2 node names contain it: the input box could no longer find them.", two.Status);
    }

    [Fact]
    public void ARename_ReplacesTheCurrentNames_OfTheSameType()
    {
        var wrote = Added("wrote", "written by");

        var plan = _planner.Rename(wrote, "authored", "authored by");

        Assert.Equal("Renames “wrote” to “authored”, read “authored by” the other way.", plan.Status);
        var change = plan.Change!;
        Assert.Equal((wrote.Type.Id, "authored", "authored by"), (change.LinkType, change.Name, change.ReverseName));
        Assert.Equal(wrote.Definitions, change.Replaces);
        _graph.Apply(change);
        Assert.Equal("authored", _graph.LinkTypes.All.Single().Type.Name);
    }

    [Fact]
    public void ARename_MayUseItsOwnPhrases()
    {
        var wrote = Added("wrote", "written by");
        _graph.Apply(new AliasAdded(Guid.NewGuid(), _clock.Now, wrote.Type.Id, false, "penned"));

        Assert.True(_planner.Rename(_graph.LinkTypes.All.Single(), "penned", "Written by").CanApply);
    }

    [Fact]
    public void ARename_KeepsTheWayItReads()
    {
        var wrote = Added("wrote", "written by");
        var knows = Added("knows", null);

        var oneWay = _planner.Rename(wrote, "authored", null);
        var bothWays = _planner.Rename(knows, "met", "met by");

        Assert.Equal(LinkTypeProblem.DirectionFixed, oneWay.Problem);
        Assert.Equal("Type how it reads the other way: links already made read both ways.", oneWay.Status);
        Assert.Equal(LinkTypeProblem.DirectionFixed, bothWays.Problem);
        Assert.Equal("Leave the other way empty: links already made read the same both ways.", bothWays.Status);
    }

    [Fact]
    public void ARenameToItsNames_ChangesNothing_UnlessItSettlesAConflict()
    {
        var knows = Added("knows", null);

        var unchanged = _planner.Rename(knows, " knows ", null);

        Assert.Equal(LinkTypeProblem.Unchanged, unchanged.Problem);
        Assert.Equal(string.Empty, unchanged.Status);

        _graph.Apply(new LinkTypeDefined(Guid.NewGuid(), _clock.Now.AddMinutes(1), knows.Type.Id, "met", null, knows.Definitions));
        _graph.Apply(new LinkTypeDefined(Guid.NewGuid(), _clock.Now.AddMinutes(2), knows.Type.Id, "knows", null, knows.Definitions));
        var conflict = _graph.LinkTypes.All.Single();
        var settled = _planner.Rename(conflict, "knows", null);

        Assert.Equal(2, settled.Change!.Replaces.Count);
        _graph.Apply(settled.Change);
        Assert.False(_graph.LinkTypes.All.Single().IsConflict);
    }

    [Fact]
    public void ATypeUsedByLinks_CannotBeRemoved()
    {
        var wrote = Added("wrote", "written by");
        var (herbert, dune, dropped) = (Add("Frank Herbert"), Add("Dune"), Add("Dropped"));
        _graph.Add(new Link(Guid.NewGuid(), wrote.Type.Id, herbert.Id, dune.Id, _clock.Now));
        var one = _planner.Remove(wrote);
        _graph.Add(new Link(Guid.NewGuid(), wrote.Type.Id, dune.Id, herbert.Id, _clock.Now));
        _graph.Add(new Link(Guid.NewGuid(), wrote.Type.Id, herbert.Id, dropped.Id, _clock.Now));
        _graph.Apply(new ChangesRemoved(Guid.NewGuid(), _clock.Now, [dropped.Id]));
        var two = _planner.Remove(wrote);

        Assert.False(one.CanRemove);
        Assert.Equal("1 link is “wrote”: change its type first.", one.Status);
        Assert.Equal(2, two.Links);
        Assert.Equal("2 links are “wrote”: change their type first.", two.Status);
    }

    [Fact]
    public void Removing_TakesItsNamesAndAliases()
    {
        Added("wrote", "written by");
        var renamed = _planner.Rename(_graph.LinkTypes.All.Single(), "authored", "authored by").Change!;
        _graph.Apply(renamed);
        var alias = new AliasAdded(Guid.NewGuid(), _clock.Now, renamed.LinkType, true, "by");
        var other = new AliasAdded(Guid.NewGuid(), _clock.Now, "relates-to", false, "-");
        _graph.Apply(alias);
        _graph.Apply(other);

        var removal = _planner.Remove(_graph.LinkTypes.All.Single());
        _graph.Apply(removal.Change!);

        Assert.Equal("Removes “authored” and its aliases.", removal.Status);
        Assert.Equal(3, removal.Change!.Removes.Count);
        Assert.Contains(alias.Id, removal.Change.Removes);
        Assert.Empty(_graph.LinkTypes.All);
        Assert.Equal([other.Id], _graph.Aliases.All.Select(kept => kept.Id));
    }

    private CustomLinkType Added(string name, string? reverse)
    {
        var change = _planner.Plan(name, reverse).Change!;
        _graph.Apply(change);
        return _graph.LinkTypes.Find(change.LinkType)!;
    }

    private Node Add(string name)
    {
        var node = new Node(Guid.NewGuid(), "generic", name, _clock.Now);
        _graph.Add(node);
        return node;
    }
}
