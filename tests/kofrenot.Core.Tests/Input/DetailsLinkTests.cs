namespace Kofrenot.Core.Tests.Input;

public sealed class DetailsLinkTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();

    private Node Add(string name, int minutes = 0)
    {
        var node = new Node(Guid.NewGuid(), "generic", name, Start.AddMinutes(minutes));
        _graph.Add(node);
        return node;
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void NothingTyped_GivesTheHint(string typed)
    {
        var link = DetailsLink.Of(_graph, Add("Dune"), typed);

        Assert.Equal((null, DetailsLink.Hint, false), (link.Request, link.Preview, link.CanAdd));
    }

    [Fact]
    public void ALinkPhraseAndANode_LinksTheNode()
    {
        var dune = Add("Dune");

        var link = DetailsLink.Of(_graph, dune, " relates to Frank Herbert ");

        Assert.True(link.CanAdd);
        Assert.Equal(("Dune", "relates to", "Frank Herbert"), (link.Request!.Left, link.Request.Verb.Name, link.Request.Right));
        Assert.Equal("Enter to add the node “Frank Herbert”, the link “Dune relates to Frank Herbert”.", link.Preview);
    }

    [Fact]
    public void AHomonym_KeepsTheNodeShown_OnTheLeft()
    {
        Add("Dune");
        var second = Add("Dune", minutes: 1);
        var arrakis = Add("Arrakis");

        var link = DetailsLink.Of(_graph, second, "contains Arrakis");

        Assert.Equal(1, link.Choices.Left);
        var plan = Assert.IsType<LinkPlan>(new AddPlanner(_graph).Plan(link.Request!, link.Choices));
        Assert.Equal((second, arrakis), (plan.Left.Chosen, plan.Right!.Chosen));
    }

    [Fact]
    public void NoLinkPhrase_IsRefused()
    {
        var link = DetailsLink.Of(_graph, Add("Dune"), "Frank Herbert");

        Assert.False(link.CanAdd);
        Assert.Null(link.Request);
        Assert.StartsWith("Start with a link phrase", link.Preview);
    }

    [Fact]
    public void ANameHoldingALinkPhrase_CannotBeLinkedThisWay()
    {
        var link = DetailsLink.Of(_graph, Add("Box contains toys"), "relates to Room");

        Assert.False(link.CanAdd);
    }

    [Fact]
    public void APhraseAlone_AsksForTheNode()
    {
        var link = DetailsLink.Of(_graph, Add("Dune"), "relates to");

        Assert.False(link.CanAdd);
        Assert.Equal("Type the node to link “Dune” to.", link.Preview);
    }

    [Fact]
    public void ALinkThatExists_IsSaid()
    {
        var (dune, herbert) = (Add("Dune"), Add("Frank Herbert"));
        _graph.Add(new Link(Guid.NewGuid(), TypeCatalog.RelatesTo.Id, dune.Id, herbert.Id, Start));

        var link = DetailsLink.Of(_graph, dune, "relates to Frank Herbert");

        Assert.False(link.CanAdd);
        Assert.Contains("already", link.Preview);
    }
}
