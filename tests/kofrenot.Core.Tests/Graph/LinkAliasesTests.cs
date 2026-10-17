namespace Kofrenot.Core.Tests.Graph;

public sealed class LinkAliasesTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();

    [Fact]
    public void WithoutAliases_TheTypesAreTheBuiltInOnes()
    {
        Assert.Same(TypeCatalog.BuiltIn, _graph.Types);
        Assert.Empty(_graph.Aliases.All);
    }

    [Fact]
    public void AnAlias_IsAddedToItsWay_AndReadByTheInputBox()
    {
        var alias = Alias("contains", reverse: true, "is in");

        Assert.True(_graph.Apply(alias));

        var contains = _graph.Types.FindLinkType("contains")!;
        Assert.Equal(["contained by", "is in"], contains.Reverse!.Phrases);
        Assert.Equal(["contains"], contains.Forward.Phrases);
        Assert.Equal(new LinkRequest("Knives", contains.Reverse, "Kitchen"), InputParser.Parse("Knives is in Kitchen", _graph.Types));
        Assert.Equal([new LinkAlias(alias.Id, TypeCatalog.Contains.Reverse!, "is in", IsUsed: true)], _graph.Aliases.All);
    }

    [Fact]
    public void ATypeWithAnAlias_IsStillEqualToTheBuiltInOne()
    {
        _graph.Apply(Alias("relates-to", reverse: false, "-"));

        Assert.Equal(TypeCatalog.RelatesTo, _graph.Types.FindLinkType("relates-to"));
        Assert.Equal(TypeCatalog.RelatesTo.GetHashCode(), _graph.Types.FindLinkType("relates-to")!.GetHashCode());
        Assert.Same(TypeCatalog.IsA, _graph.Types.FindLinkType("is-a"));
    }

    [Fact]
    public void AnAliasAppliedTwice_ChangesNothing()
    {
        var alias = Alias("relates-to", reverse: false, "-");
        _graph.Apply(alias);

        Assert.False(_graph.Apply(alias));
        Assert.Single(_graph.Aliases.All);
    }

    [Fact]
    public void ARemovedAlias_IsNoLongerRead()
    {
        var alias = Alias("relates-to", reverse: false, "-");
        _graph.Apply(alias);
        var removal = new ChangesRemoved(Guid.NewGuid(), Start.AddMinutes(1), [alias.Id]);

        Assert.True(_graph.Apply(removal));
        Assert.False(_graph.Apply(removal));

        Assert.Empty(_graph.Aliases.All);
        Assert.Equal(new NodeRequest("Dune - Herbert"), InputParser.Parse("Dune - Herbert", _graph.Types));
    }

    [Fact]
    public void ARemovalArrivingFirst_StillRemoves()
    {
        var alias = Alias("relates-to", reverse: false, "-");
        _graph.Apply(new ChangesRemoved(Guid.NewGuid(), Start.AddMinutes(1), [alias.Id]));

        _graph.Apply(alias);

        Assert.Empty(_graph.Aliases.All);
    }

    [Fact]
    public void TheSamePhrase_ForTwoTypes_IsUsedByTheOldestAliasOnly()
    {
        var first = Alias("relates-to", reverse: false, "with", minutes: 1);
        var second = Alias("contains", reverse: false, "WITH", minutes: 2);
        _graph.Apply(second);
        _graph.Apply(first);

        Assert.Equal([(first.Id, true), (second.Id, false)], _graph.Aliases.All.Select(alias => (alias.Id, alias.IsUsed)));
        Assert.Equal(TypeCatalog.RelatesTo.Forward, ((LinkRequest)InputParser.Parse("a with b", _graph.Types)!).Verb);
    }

    [Fact]
    public void AnAliasOfABuiltInPhrase_IsNotUsed()
    {
        _graph.Apply(Alias("contains", reverse: false, "relates to"));

        Assert.False(Assert.Single(_graph.Aliases.All).IsUsed);
        Assert.Equal(["contains"], _graph.Types.FindLinkType("contains")!.Phrases);
    }

    [Fact]
    public void AnAliasOfAnUnknownType_OrOfTheReverseOfASymmetricOne_IsKeptButNotShown()
    {
        _graph.Apply(Alias("written-by", reverse: false, "by"));
        _graph.Apply(Alias("relates-to", reverse: true, "near"));

        Assert.Empty(_graph.Aliases.All);
        Assert.IsType<NodeRequest>(InputParser.Parse("Dune by Herbert", _graph.Types));
    }

    [Fact]
    public void ByVerb_ListsEachWay_WithItsBuiltInPhrases_AndAliases()
    {
        var alias = Alias("contains", reverse: true, "is in");
        _graph.Apply(alias);

        var verbs = _graph.Aliases.ByVerb();

        Assert.Equal(["relates to", "contains", "contained by", "is a", "has instance"], verbs.Select(verb => verb.Verb.Name));
        Assert.Equal(["relates to", "related to"], verbs[0].BuiltIn);
        Assert.Equal(["contained by"], verbs[2].BuiltIn);
        Assert.Equal([alias.Id], verbs[2].Aliases.Select(row => row.Id));
        Assert.All(verbs.Where(verb => verb.Verb != TypeCatalog.Contains.Reverse), verb => Assert.Empty(verb.Aliases));
    }

    private static AliasAdded Alias(string type, bool reverse, string phrase, int minutes = 0) =>
        new(Guid.NewGuid(), Start.AddMinutes(minutes), type, reverse, phrase);
}
