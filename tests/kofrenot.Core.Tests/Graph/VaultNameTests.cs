namespace Kofrenot.Core.Tests.Graph;

public sealed class VaultNameTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();

    private static VaultNamed Name(string name, int minutes, params Guid[] replaces) =>
        new(Guid.NewGuid(), Start.AddMinutes(minutes), name, replaces);

    [Fact]
    public void AVaultNotNamed_HasNoName()
    {
        Assert.Empty(_graph.VaultNames);
        Assert.Null(_graph.VaultName);
    }

    [Fact]
    public void ANamedVault_ShowsItsName_AppliedOnce()
    {
        var name = Name("Personal", 1);

        Assert.True(_graph.Apply(name));
        Assert.False(_graph.Apply(name));

        Assert.Equal("Personal", _graph.VaultName);
        Assert.Equal([name], _graph.VaultNames);
    }

    [Fact]
    public void NamesInARow_ShowTheLast_InAnyOrder()
    {
        var first = Name("Personal", 1);
        var second = Name("Private", 2, first.Id);

        _graph.Apply(second);
        _graph.Apply(first);

        Assert.Equal("Private", _graph.VaultName);
        Assert.Equal([second], _graph.VaultNames);
    }

    [Fact]
    public void NamesMadeApart_AreAConflict_ShowingTheLatest()
    {
        var mine = Name("Work", 1);
        var theirs = Name("Office", 2);

        _graph.Apply(theirs);
        _graph.Apply(mine);

        Assert.Equal(["Work", "Office"], _graph.VaultNames.Select(name => name.Name));
        Assert.Equal("Office", _graph.VaultName);
    }

    [Fact]
    public void ANameReplacingBoth_ResolvesTheConflict()
    {
        var mine = Name("Work", 1);
        var theirs = Name("Office", 2);
        _graph.Apply(mine);
        _graph.Apply(theirs);

        _graph.Apply(Name("Work", 3, mine.Id, theirs.Id));

        Assert.Single(_graph.VaultNames);
        Assert.Equal("Work", _graph.VaultName);
    }
}
