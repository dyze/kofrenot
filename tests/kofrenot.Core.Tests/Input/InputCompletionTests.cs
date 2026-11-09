namespace Kofrenot.Core.Tests.Input;

public sealed class InputCompletionTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NothingTyped_ProposesNothing(string typed) =>
        Assert.Empty(InputCompletion.Proposals(_graph, typed, linkTypeTyped: false));

    [Fact]
    public void ANodeName_IsCompleted_TheShortestFirst_InItsOwnCase()
    {
        Add("Dune Messiah");
        Add("Dune");
        Add("Frank Herbert");

        Assert.Equal(new Proposal("ne", "Dune", IsLinkType: false), InputCompletion.Proposals(_graph, " du", linkTypeTyped: false).FirstOrDefault());
        Assert.Equal(new Proposal(" Messiah", "Dune Messiah", IsLinkType: false), InputCompletion.Proposals(_graph, "dune", linkTypeTyped: false).FirstOrDefault());
    }

    [Fact]
    public void NamesOfTheSameLength_AreProposedByName()
    {
        Add("obj2");
        Add("obj1");

        Assert.Equal("obj1", InputCompletion.Proposals(_graph, "ob", linkTypeTyped: false).FirstOrDefault()?.Label);
    }

    [Fact]
    public void AWholeName_IsNotProposedAgain()
    {
        Add("Dune");

        Assert.Empty(InputCompletion.Proposals(_graph, "Dune", linkTypeTyped: true));
    }

    [Theory]
    [InlineData("obj1 i", "s a", "is a", 1)]
    [InlineData("obj1 is ", "a", "is a", 1)]
    [InlineData("Old map c", "ontains", "contains", 2)]
    [InlineData("Knives contained b", "y", "contained by", 1)]
    [InlineData("Dune RELATES T", "o", "relates to", 1)]
    public void ALinkType_IsCompletedAfterAWord(string typed, string rest, string label, int wordsBefore) =>
        Assert.Equal(new Proposal(rest, label, IsLinkType: true, wordsBefore), InputCompletion.Proposals(_graph, typed, linkTypeTyped: false).FirstOrDefault());

    [Theory]
    [InlineData("is")]
    [InlineData("obj1 is a")]
    [InlineData("obj1 xyz")]
    public void NoLinkType_IsProposedForTheFirstWord_ACompleteOne_OrNoMatch(string typed) =>
        Assert.Empty(InputCompletion.Proposals(_graph, typed, linkTypeTyped: false));

    [Fact]
    public void ANode_IsProposedBeforeALinkType()
    {
        Add("Dune relics");

        Assert.Equal("Dune relics", InputCompletion.Proposals(_graph, "Dune rel", linkTypeTyped: false).FirstOrDefault()?.Label);
    }

    [Fact]
    public void OnceTheLinkTypeIsALabel_OnlyNodesAreProposed() =>
        Assert.Empty(InputCompletion.Proposals(_graph, "obj1 i", linkTypeTyped: true));

    [Fact]
    public void AllNamesStartingWithTheText_AreProposed_TheShortestFirst_ThenTheLinkTypes()
    {
        Add("Kitchen table");
        Add("Kitchen");
        Add("Kitchen drawer");
        Add("Bathroom");

        Assert.Equal(
            ["Kitchen", "Kitchen table", "Kitchen drawer"],
            InputCompletion.Proposals(_graph, "kit", linkTypeTyped: false).Select(proposal => proposal.Label));
        Assert.Equal(
            ["contains", "contained by"],
            InputCompletion.Proposals(_graph, "Kitchen con", linkTypeTyped: false).Select(proposal => proposal.Label));
    }

    [Fact]
    public void AfterAFixedNode_EachLinkTypeStartingWithTheText_IsProposed() =>
        Assert.Equal(
            ["contains", "contained by"],
            InputCompletion.LinkTypeProposalsAfter(_graph.Types, "Old map", "con").Select(proposal => proposal.Label));

    [Theory]
    [InlineData("read", " by")]
    [InlineData("READ ", "by")]
    public void ACompleteLinkType_ProposesTheLongerOnesStartingWithIt(string typed, string rest)
    {
        var types = new TypeCatalog([TypeCatalog.Generic], [new LinkType("read", ["read"], ["read by"])]);

        Assert.Equal(
            [new Proposal(rest, "read by", IsLinkType: true, WordsBefore: 1)],
            InputCompletion.LinkTypeProposalsAfter(types, "Dune", typed));
    }

    [Fact]
    public void ACompleteLinkType_WithNoLongerOne_IsNotCompletedFromItsLastWords()
    {
        var types = new TypeCatalog([TypeCatalog.Generic], [new LinkType("read", ["read"], ["read by"])]);

        Assert.Empty(InputCompletion.Proposals(_graph, "Dune contained by", linkTypeTyped: false));
        Assert.Empty(InputCompletion.LinkTypeProposalsAfter(types, "Dune", "read by"));
    }

    private void Add(string name) => _graph.Add(new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, name, Start));
}
