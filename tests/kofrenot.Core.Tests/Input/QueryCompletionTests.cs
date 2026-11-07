namespace Kofrenot.Core.Tests.Input;

public sealed class QueryCompletionTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();

    public QueryCompletionTests()
    {
        Add("Kitchen", TypeCatalog.Generic);
        Add("Kitchen table", TypeCatalog.Generic);
        Add("Ideas", TypeCatalog.Note);
    }

    [Theory]
    [InlineData("?no", "te: ")]
    [InlineData("?Note", ": ")]
    [InlineData("?ch", "anged: ")]
    [InlineData("? ad", "ded: ")]
    [InlineData("?con", "tains ")]
    [InlineData("?contained b", "y ")]
    public void TheFirstWord_IsATypeATimeOrALinkPhrase_TheShortestFirst(string text, string rest)
    {
        var proposal = QueryCompletion.Proposals(_graph, text).FirstOrDefault();

        Assert.Equal(rest, proposal?.Rest);
        Assert.True(proposal?.IsQueryText);
    }

    [Theory]
    [InlineData("?changed: ", "today")]
    [InlineData("?changed: this", " week")]
    [InlineData("?opened: Y", "esterday")]
    [InlineData("?note: changed: to", "day")]
    public void AfterATime_IsAPeriod(string text, string rest) =>
        Assert.Equal(rest, QueryCompletion.Proposals(_graph, text).FirstOrDefault()?.Rest);

    [Theory]
    [InlineData("?note: ch", "anged: ")]
    [InlineData("?note: con", "tains ")]
    [InlineData("?note: Id", "eas")]
    [InlineData("?contained by Kit", "chen")]
    [InlineData("?contained by Kitchen ", "table")]
    public void AfterATypeOrALinkPhrase_IsTheNextWord_OrANode(string text, string rest)
    {
        var proposal = QueryCompletion.Proposals(_graph, text).FirstOrDefault();

        Assert.Equal(rest, proposal?.Rest);
        Assert.True(proposal?.IsQueryText);
    }

    [Fact]
    public void AfterATime_EachPeriodIsProposed() =>
        Assert.Equal(
            ["today", "this week", "this year", "yesterday", "this month"],
            QueryCompletion.Proposals(_graph, "?changed: ").Select(proposal => proposal.Label));

    [Fact]
    public void AfterAType_TheNextWordsComeBeforeItsNodes()
    {
        Add("Contacts", TypeCatalog.Note);

        Assert.Equal(["contains ", "contained by ", "Contacts"], QueryCompletion.Proposals(_graph, "?note: con").Select(proposal => proposal.Label));
    }

    [Theory]
    [InlineData("?")]
    [InlineData("?note: ")]
    [InlineData("?note: Kit")]
    [InlineData("?changed:")]
    [InlineData("?changed: 2025")]
    [InlineData("?contained by ")]
    [InlineData("?contained by Nothing")]
    [InlineData("?xyz")]
    public void NothingIsProposed_WhenNothingFits(string text) => Assert.Empty(QueryCompletion.Proposals(_graph, text));

    [Fact]
    public void TheStarts_AreEachType_WithItsNodes_EachTime_AndEachLinkPhrase()
    {
        var starts = QueryCompletion.Starts(_graph);

        Assert.Contains(new QueryStart("?Generic: ", "2 nodes"), starts);
        Assert.Contains(new QueryStart("?Note: ", "1 node"), starts);
        Assert.Contains(new QueryStart("?Dataset: ", "0 nodes"), starts);
        Assert.Contains(new QueryStart("?changed: ", "the nodes by their last change"), starts);
        Assert.Contains(new QueryStart("?contained by ", "the nodes linked so to a node"), starts);
    }

    private void Add(string name, NodeType type) => _graph.Add(new Node(Guid.NewGuid(), type.Id, name, Start));
}
