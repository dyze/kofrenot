namespace Kofrenot.Core.Tests.Input;

public sealed class VaultNamePlannerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();
    private readonly ManualClock _clock = new() { Now = Start };

    private VaultNamePlan Plan(string text) => new VaultNamePlanner(_graph, _clock).Plan(text);

    [Fact]
    public void AFirstName_ReplacesNothing()
    {
        var plan = Plan("  Personal ");

        Assert.Equal(VaultNameProblem.None, plan.Problem);
        Assert.Equal("Personal", plan.Change!.Name);
        Assert.Equal(Start, plan.Change.At);
        Assert.Empty(plan.Change.Replaces);
    }

    [Fact]
    public void ANewName_ReplacesTheCurrentOne()
    {
        var first = Plan("Personal").Change!;
        _graph.Apply(first);

        var plan = Plan("Private");

        Assert.Equal([first.Id], plan.Change!.Replaces);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NoName_IsRefused(string text)
    {
        var plan = Plan(text);

        Assert.Equal(VaultNameProblem.Empty, plan.Problem);
        Assert.Null(plan.Change);
    }

    [Fact]
    public void TheCurrentName_IsUnchanged()
    {
        _graph.Apply(Plan("Personal").Change!);

        Assert.Equal(VaultNameProblem.Unchanged, Plan(" Personal").Problem);
    }

    [Fact]
    public void NamingAVaultInConflict_ReplacesEveryName_EvenWithOneOfThem()
    {
        var a = new VaultNamed(Guid.NewGuid(), Start, "Work", []);
        var b = new VaultNamed(Guid.NewGuid(), Start.AddMinutes(1), "Office", []);
        _graph.Apply(a);
        _graph.Apply(b);

        var plan = Plan("Office");

        Assert.Equal(VaultNameProblem.None, plan.Problem);
        Assert.Equal([a.Id, b.Id], plan.Change!.Replaces);
    }
}
