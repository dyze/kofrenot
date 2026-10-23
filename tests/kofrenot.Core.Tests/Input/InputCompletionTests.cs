namespace Kofrenot.Core.Tests.Input;

public sealed class InputCompletionTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NothingTyped_ProposesNothing(string typed) =>
        Assert.Null(InputCompletion.Propose(_graph, typed, linkTypeTyped: false));

    [Fact]
    public void ANodeName_IsCompleted_TheShortestFirst_InItsOwnCase()
    {
        Add("Dune Messiah");
        Add("Dune");
        Add("Frank Herbert");

        Assert.Equal(new Proposal("ne", "Dune", IsLinkType: false), InputCompletion.Propose(_graph, " du", linkTypeTyped: false));
        Assert.Equal(new Proposal(" Messiah", "Dune Messiah", IsLinkType: false), InputCompletion.Propose(_graph, "dune", linkTypeTyped: false));
    }

    [Fact]
    public void NamesOfTheSameLength_AreProposedByName()
    {
        Add("obj2");
        Add("obj1");

        Assert.Equal("obj1", InputCompletion.Propose(_graph, "ob", linkTypeTyped: false)?.Label);
    }

    [Fact]
    public void AWholeName_IsNotProposedAgain()
    {
        Add("Dune");

        Assert.Null(InputCompletion.Propose(_graph, "Dune", linkTypeTyped: true));
    }

    [Theory]
    [InlineData("obj1 i", "s a", "is a", 1)]
    [InlineData("obj1 is ", "a", "is a", 1)]
    [InlineData("Old map c", "ontains", "contains", 2)]
    [InlineData("Knives contained b", "y", "contained by", 1)]
    [InlineData("Dune RELATES T", "o", "relates to", 1)]
    public void ALinkType_IsCompletedAfterAWord(string typed, string rest, string label, int wordsBefore) =>
        Assert.Equal(new Proposal(rest, label, IsLinkType: true, wordsBefore), InputCompletion.Propose(_graph, typed, linkTypeTyped: false));

    [Theory]
    [InlineData("is")]
    [InlineData("obj1 is a")]
    [InlineData("obj1 xyz")]
    public void NoLinkType_IsProposedForTheFirstWord_ACompleteOne_OrNoMatch(string typed) =>
        Assert.Null(InputCompletion.Propose(_graph, typed, linkTypeTyped: false));

    [Fact]
    public void ANode_IsProposedBeforeALinkType()
    {
        Add("Dune relics");

        Assert.Equal("Dune relics", InputCompletion.Propose(_graph, "Dune rel", linkTypeTyped: false)?.Label);
    }

    [Fact]
    public void OnceTheLinkTypeIsALabel_OnlyNodesAreProposed() =>
        Assert.Null(InputCompletion.Propose(_graph, "obj1 i", linkTypeTyped: true));

    private void Add(string name) => _graph.Add(new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, name, Start));
}
