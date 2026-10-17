namespace Kofrenot.Core.Tests.Input;

public sealed class AddPlannerTests
{
    private readonly ManualClock _clock = new();
    private readonly KnowledgeGraph _graph = new();
    private readonly AddPlanner _planner;

    public AddPlannerTests() => _planner = new AddPlanner(_graph, _clock);

    [Fact]
    public void ANewName_AddsANode_WithoutConfirmation()
    {
        var plan = Assert.IsType<NodePlan>(_planner.Plan(new NodeRequest("Dune")));

        Assert.False(plan.NeedsConfirmation);
        Assert.True(plan.CanAdd);
        var added = Assert.IsType<NodeAdded>(Assert.Single(plan.Changes));
        Assert.Equal(new Node(added.Node.Id, "generic", "Dune", _clock.Now), added.Node);
        Assert.Equal(_clock.Now, added.At);
    }

    [Fact]
    public void AnExistingName_AsksForConfirmation()
    {
        var dune = Add("Dune");

        var plan = Assert.IsType<NodePlan>(_planner.Plan(new NodeRequest("DUNE")));

        Assert.True(plan.NeedsConfirmation);
        Assert.Equal([dune], plan.Existing);
    }

    [Fact]
    public void ALinkBetweenNewNames_AddsBothNodes_ThenTheLink()
    {
        var plan = PlanLink("Dune", "Frank Herbert");

        Assert.Equal(LinkProblem.None, plan.Problem);
        Assert.True(plan.Left.IsNew);
        Assert.Equal(3, plan.Changes.Count);
        var nodes = plan.Changes.OfType<NodeAdded>().Select(added => added.Node).ToList();
        Assert.Equal(["Dune", "Frank Herbert"], nodes.Select(node => node.Name));
        var link = Assert.IsType<LinkAdded>(plan.Changes[2]).Link;
        Assert.Equal((nodes[0].Id, nodes[1].Id, "relates-to"), (link.From, link.To, link.Type));
        Assert.False(plan.NeedsConfirmation);
    }

    [Fact]
    public void ALinkToAnExistingNode_UsesIt()
    {
        var dune = Add("Dune");

        var plan = PlanLink("dune", "Frank Herbert");

        Assert.Same(dune, plan.Left.Chosen);
        Assert.Equal(2, plan.Changes.Count);
        Assert.Equal(dune.Id, plan.Changes.OfType<LinkAdded>().Single().Link.From);
    }

    [Fact]
    public void AmongHomonyms_TheOldestIsChosen_UnlessAnotherIsPicked()
    {
        var older = Add("Dune", minutes: 1);
        var newer = Add("Dune", minutes: 2);

        Assert.Same(older, PlanLink("Dune", "Arrakis").Left.Chosen);
        var picked = PlanLink("Dune", "Arrakis", leftChoice: 1);
        Assert.Same(newer, picked.Left.Chosen);
        Assert.Equal(newer.Id, picked.Changes.OfType<LinkAdded>().Single().Link.From);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(5, 1)]
    public void AChoiceOutOfRange_IsBroughtBack(int choice, int expected)
    {
        Add("Dune", minutes: 1);
        Add("Dune", minutes: 2);

        Assert.Equal(expected, PlanLink("Dune", "Arrakis", leftChoice: choice).Left.Choice);
        Assert.Equal(0, PlanLink("Arrakis", "Dune", rightChoice: choice).Left.Choice);
    }

    [Fact]
    public void TheRightSide_HasItsOwnChoice()
    {
        Add("Dune", minutes: 1);
        var newer = Add("Dune", minutes: 2);

        Assert.Same(newer, PlanLink("Arrakis", "Dune", rightChoice: 1).Right?.Chosen);
    }

    [Fact]
    public void AnExistingLink_IsNotAddedAgain_EvenTypedTheOtherWay()
    {
        var (dune, herbert) = (Add("Dune"), Add("Frank Herbert"));
        var link = new Link(Guid.NewGuid(), "relates-to", dune.Id, herbert.Id, _clock.Now);
        _graph.Add(link);

        var plan = PlanLink("Frank Herbert", "Dune");

        Assert.Equal(LinkProblem.AlreadyLinked, plan.Problem);
        Assert.Same(link, plan.Existing);
        Assert.False(plan.CanAdd);
        Assert.False(plan.NeedsConfirmation);
    }

    [Fact]
    public void ALinkWithoutItsSecondNode_CannotBeAddedYet()
    {
        var plan = Assert.IsType<LinkPlan>(_planner.Plan(new LinkRequest("Dune", TypeCatalog.RelatesTo.Forward, "")));

        Assert.Equal(LinkProblem.SecondNodeMissing, plan.Problem);
        Assert.Null(plan.Right);
        Assert.False(plan.CanAdd);
    }

    [Fact]
    public void ALinkOfANewNameToItself_CannotBeAdded() =>
        Assert.Equal(LinkProblem.SameNode, PlanLink("Dune", "dune").Problem);

    [Fact]
    public void ALinkOfANodeToItself_CannotBeAdded()
    {
        Add("Dune");

        var plan = PlanLink("Dune", "DUNE");

        Assert.Equal(LinkProblem.SameNode, plan.Problem);
        Assert.Empty(plan.Changes);
    }

    [Fact]
    public void ALinkBetweenTwoHomonyms_CanBeAdded()
    {
        var older = Add("Dune", minutes: 1);
        var newer = Add("Dune", minutes: 2);

        var plan = PlanLink("Dune", "Dune", rightChoice: 1);

        Assert.Equal(LinkProblem.None, plan.Problem);
        var link = Assert.Single(plan.Changes.OfType<LinkAdded>()).Link;
        Assert.Equal((older.Id, newer.Id), (link.From, link.To));
    }

    [Fact]
    public void ANewNode_HasTheChosenType()
    {
        var book = new NodeType("book", "Book");

        var plan = Assert.IsType<NodePlan>(_planner.Plan(new NodeRequest("Dune"), new AddChoices(LeftType: book)));

        Assert.Same(book, plan.Type);
        Assert.Equal("book", Assert.IsType<NodeAdded>(Assert.Single(plan.Changes)).Node.Type);
    }

    [Fact]
    public void TheNewNodesOfALink_HaveTheirSidesTypes()
    {
        var (book, person) = (new NodeType("book", "Book"), new NodeType("person", "Person"));

        var plan = Assert.IsType<LinkPlan>(_planner.Plan(
            new LinkRequest("Dune", TypeCatalog.RelatesTo.Forward, "Frank Herbert"), new AddChoices(LeftType: book, RightType: person)));

        Assert.Equal(["book", "person"], plan.Changes.OfType<NodeAdded>().Select(added => added.Node.Type));
    }

    [Fact]
    public void ByDefault_NewNodesAreGeneric()
    {
        var plan = PlanLink("Dune", "Frank Herbert");

        Assert.Same(TypeCatalog.Generic, plan.Left.Type);
        Assert.Same(TypeCatalog.Generic, plan.Right!.Type);
    }

    [Fact]
    public void ALinkOfAnotherType_BetweenLinkedNodes_CanBeAdded()
    {
        var types = new TypeCatalog([TypeCatalog.Generic], [TypeCatalog.RelatesTo, WrittenBy]);
        var graph = new KnowledgeGraph(types);
        var (dune, herbert) = (Add(graph, "Dune"), Add(graph, "Frank Herbert"));
        graph.Add(new Link(Guid.NewGuid(), "relates-to", dune.Id, herbert.Id, _clock.Now));

        var plan = Assert.IsType<LinkPlan>(new AddPlanner(graph, _clock).Plan(new LinkRequest("Dune", WrittenBy.Forward, "Frank Herbert")));

        Assert.Equal(LinkProblem.None, plan.Problem);
        Assert.Equal("written-by", plan.Changes.OfType<LinkAdded>().Single().Link.Type);
    }

    [Fact]
    public void AReverseLink_GoesFromTheRightNode_ToTheLeftOne()
    {
        var kitchen = Add("Kitchen");

        var plan = Assert.IsType<LinkPlan>(_planner.Plan(new LinkRequest("Knives", TypeCatalog.Contains.Reverse!, "Kitchen")));

        var knives = plan.Changes.OfType<NodeAdded>().Single().Node;
        var link = plan.Changes.OfType<LinkAdded>().Single().Link;
        Assert.Equal(("contains", kitchen.Id, knives.Id), (link.Type, link.From, link.To));
        Assert.Same(TypeCatalog.Contains, plan.Type);
    }

    [Fact]
    public void ADirectedLink_TypedEitherWay_IsFoundAlreadyThere()
    {
        var (kitchen, knives) = (Add("Kitchen"), Add("Knives"));
        _graph.Add(new Link(Guid.NewGuid(), "contains", kitchen.Id, knives.Id, _clock.Now));

        var reverse = Assert.IsType<LinkPlan>(_planner.Plan(new LinkRequest("Knives", TypeCatalog.Contains.Reverse!, "Kitchen")));
        var opposite = Assert.IsType<LinkPlan>(_planner.Plan(new LinkRequest("Knives", TypeCatalog.Contains.Forward, "Kitchen")));

        Assert.Equal(LinkProblem.AlreadyLinked, reverse.Problem);
        Assert.Equal(LinkProblem.None, opposite.Problem);
    }

    [Fact]
    public void AnUnknownRequest_IsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _planner.Plan(new OtherRequest()));

    private LinkPlan PlanLink(string left, string right, int leftChoice = 0, int rightChoice = 0) =>
        Assert.IsType<LinkPlan>(_planner.Plan(new LinkRequest(left, TypeCatalog.RelatesTo.Forward, right), new AddChoices(leftChoice, rightChoice)));

    private static readonly LinkType WrittenBy = new("written-by", ["written by"], ["wrote"]);

    private Node Add(string name, int minutes = 0) => Add(_graph, name, minutes);

    private Node Add(KnowledgeGraph graph, string name, int minutes = 0)
    {
        var node = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, name, _clock.Now.AddMinutes(minutes));
        graph.Add(node);
        return node;
    }

    private sealed record OtherRequest : InputRequest;
}
