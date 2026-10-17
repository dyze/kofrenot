namespace Kofrenot.Core.Tests.Input;

public sealed class CreationFormTests
{
    private static readonly NodeType Book = new("book", "Book");
    private static readonly NodeType Person = new("person", "Person");
    private static readonly LinkType WrittenBy = new("written-by", ["written by"], ["wrote"]);
    private static readonly TypeCatalog Types = new([TypeCatalog.Generic, Book, Person], [TypeCatalog.RelatesTo, WrittenBy]);

    private readonly ManualClock _clock = new();
    private readonly KnowledgeGraph _graph = new(Types);

    [Fact]
    public void ANode_IsGenericByDefault()
    {
        var form = Form(new NodeRequest("Dune"));

        Assert.False(form.IsLink);
        Assert.Equal("Add a node", form.Title);
        Assert.Same(TypeCatalog.Generic, form.LeftType);
        Assert.Null(form.Verb);
        Assert.Equal("Adds the node “Dune”.", form.Status);
        Assert.Null(form.Warning);
        Assert.Empty(form.Existing);
        Assert.Equal(Types, form.Types);
    }

    [Fact]
    public void TheBuiltInTypes_AreUsedByDefault() =>
        Assert.Same(TypeCatalog.BuiltIn, new CreationForm(new KnowledgeGraph(), new NodeRequest("Dune")).Types);

    [Fact]
    public void ChangingTheNodeType_PlansAgain()
    {
        var form = Form(new NodeRequest("Dune"));

        form.LeftType = Book;

        Assert.Same(Book, form.LeftType);
        Assert.Equal("book", Assert.IsType<NodeAdded>(Assert.Single(form.Plan.Changes)).Node.Type);
    }

    [Fact]
    public void AnExistingName_IsShownWithItsTypes_AndWarnedAbout()
    {
        Add("Dune", Book.Id, minutes: 1);
        Add("Dune", "unknown", minutes: 2);

        var form = Form(new NodeRequest("dune"));

        Assert.Equal(["Dune¹ (Book)", "Dune² (unknown)"], form.Existing);
        Assert.Equal("“dune” exists already (2 times). Add makes another node with this name.", form.Warning);
        Assert.True(form.Plan.CanAdd);
    }

    [Fact]
    public void ALink_GivesEachNewNodeItsType_AndCanChangeTheLinkType()
    {
        var form = Form(new LinkRequest("Dune", TypeCatalog.RelatesTo.Forward, "Frank Herbert"));

        form.LeftType = Book;
        form.RightType = Person;
        form.Verb = WrittenBy.Forward;

        Assert.True(form.IsLink);
        Assert.Equal("Add a link", form.Title);
        Assert.Same(Person, form.RightType);
        Assert.Equal(WrittenBy.Forward, form.Verb);
        Assert.Equal(["book", "person"], form.Plan.Changes.OfType<NodeAdded>().Select(added => added.Node.Type));
        Assert.Equal("written-by", form.Plan.Changes.OfType<LinkAdded>().Single().Link.Type);
        Assert.Equal("Adds the node “Dune”, the node “Frank Herbert”, the link “Dune written by Frank Herbert”.", form.Status);
    }

    [Fact]
    public void ALink_KeepsTheHomonymChosenInTheInputBox()
    {
        Add("Dune", minutes: 1);
        var newer = Add("Dune", minutes: 2);

        var form = Form(new LinkRequest("Dune", TypeCatalog.RelatesTo.Forward, "Arrakis"), new AddChoices(Left: 1));
        form.RightType = Book;

        var link = Assert.IsType<LinkPlan>(form.Plan);
        Assert.Same(newer, link.Left.Chosen);
        Assert.Equal("Dune²", form.Label(link.Left));
        Assert.Equal("Arrakis", form.Label(link.Right!));
    }

    [Fact]
    public void EachSide_SaysWhetherItExists()
    {
        Add("Dune", Book.Id);
        Add("Frank Herbert", minutes: 1);
        Add("Frank Herbert", minutes: 2);

        var exists = Assert.IsType<LinkPlan>(Form(new LinkRequest("Dune", TypeCatalog.RelatesTo.Forward, "Frank Herbert")).Plan);
        var form = Form(new LinkRequest("Dune", TypeCatalog.RelatesTo.Forward, "Arrakis"));
        var created = Assert.IsType<LinkPlan>(form.Plan);

        Assert.Equal("Exists already (Book)", form.Describe(exists.Left));
        Assert.Equal("Exists already (Generic), one of 2 nodes with this name", form.Describe(exists.Right!));
        Assert.Equal("New node", form.Describe(created.Right!));
    }

    [Fact]
    public void ALinkTypeAlreadyThere_CannotBeAdded_ButAnotherOneCan()
    {
        var (dune, herbert) = (Add("Dune"), Add("Frank Herbert"));
        _graph.Add(new Link(Guid.NewGuid(), WrittenBy.Id, dune.Id, herbert.Id, _clock.Now));
        var form = Form(new LinkRequest("Dune", WrittenBy.Forward, "Frank Herbert"));

        Assert.False(form.Plan.CanAdd);
        Assert.Equal("“Dune” written by “Frank Herbert” already.", form.Status);

        form.Verb = TypeCatalog.RelatesTo.Forward;

        Assert.True(form.Plan.CanAdd);
        Assert.Equal("Adds the link “Dune relates to Frank Herbert”.", form.Status);
    }

    [Fact]
    public void TheLinkType_CanBeTurnedTheOtherWay()
    {
        var form = Form(new LinkRequest("Dune", WrittenBy.Forward, "Frank Herbert"));

        form.Verb = WrittenBy.Reverse;

        var link = form.Plan.Changes.OfType<LinkAdded>().Single().Link;
        var nodes = form.Plan.Changes.OfType<NodeAdded>().Select(added => added.Node).ToList();
        Assert.Equal((nodes[1].Id, nodes[0].Id), (link.From, link.To));
        Assert.Equal("Adds the node “Dune”, the node “Frank Herbert”, the link “Dune wrote Frank Herbert”.", form.Status);
        Assert.Equal(["relates to", "written by", "wrote"], form.Types.Verbs.Select(verb => verb.Name));
    }

    [Fact]
    public void ANode_HasNoLinkTypeToSet()
    {
        var form = Form(new NodeRequest("Dune"));

        Assert.Throws<InvalidOperationException>(() => form.Verb = WrittenBy.Forward);
        Assert.Throws<ArgumentNullException>(() => form.Verb = null);
    }

    private CreationForm Form(InputRequest request, AddChoices? choices = null) => new(_graph, request, choices, Types, _clock);

    private Node Add(string name, string? type = null, int minutes = 0)
    {
        var node = new Node(Guid.NewGuid(), type ?? TypeCatalog.Generic.Id, name, _clock.Now.AddMinutes(minutes));
        _graph.Add(node);
        return node;
    }
}
