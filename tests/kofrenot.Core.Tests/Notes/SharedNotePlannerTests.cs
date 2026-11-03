namespace Kofrenot.Core.Tests.Notes;

public sealed class SharedNotePlannerTests
{
    private readonly ManualClock _clock = new();
    private readonly KnowledgeGraph _graph = new();

    [Theory]
    [InlineData("Buy spice\nand water", "Buy spice")]
    [InlineData("\n  \n# Dune notes\r\ntext", "Dune notes")]
    [InlineData("https://example.com/dune", "https://example.com/dune")]
    [InlineData("   \n", SharedNotePlanner.DefaultName)]
    [InlineData("", SharedNotePlanner.DefaultName)]
    public void TheName_IsTheFirstLineWithText(string text, string name) =>
        Assert.Equal(name, SharedNotePlanner.NameOf(text));

    [Fact]
    public void ALongFirstLine_IsCut()
    {
        var name = SharedNotePlanner.NameOf(new string('a', 200));

        Assert.Equal(SharedNotePlanner.MaxName, name.Length);
        Assert.EndsWith("…", name);
    }

    [Fact]
    public void ThePreview_KeepsTheWholeText_AndFlagsANameTaken()
    {
        _graph.Add(new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Dune", _clock.Now));
        var planner = new SharedNotePlanner(_graph, _clock);

        var note = planner.Preview("Dune\nby Frank Herbert");

        Assert.Equal(new SharedNote("Dune", "Dune\nby Frank Herbert", IsHomonym: true), note);
        Assert.False(planner.Preview("Arrakis").IsHomonym);
    }

    [Fact]
    public void ThePlan_AddsANote_WithTheTextAsItsFirstVersion()
    {
        var plan = new SharedNotePlanner(_graph, _clock).Plan(new SharedNote("Dune", "Dune\ntext", false));

        var added = Assert.IsType<NodeAdded>(Assert.Single(plan.Changes)).Node;
        Assert.Equal(plan.Node, added);
        Assert.Equal(("Dune", TypeCatalog.Note.Id, _clock.Now), (added.Name, added.Type, added.Created));
        Assert.Equal((added.Id, "Dune\ntext"), (plan.Note.Version.Node, plan.Note.Content));
        Assert.Empty(plan.Note.Version.Replaces);
    }

    [Fact]
    public void UnderADestination_OnlyANameTakenUnderIt_IsAHomonym()
    {
        var parent = Add("Inbox");
        Add("Arrakis");
        var dune = Add("dune");
        _graph.Apply(new LinkAdded(Guid.NewGuid(), _clock.Now, new Link(Guid.NewGuid(), TypeCatalog.Contains.Id, parent.Id, dune.Id, _clock.Now)));
        var planner = new SharedNotePlanner(_graph, _clock);
        var under = new ParentLink(parent, TypeCatalog.Contains.Id);

        Assert.True(planner.Preview("Dune", under).IsHomonym);
        Assert.False(planner.Preview("Arrakis", under).IsHomonym);
        Assert.Equal(under, planner.Preview("Arrakis", under).Under);
    }

    [Fact]
    public void UnderADestination_ThePlanLinksTheNoteUnderIt_ByItsLinkType()
    {
        var parent = Add("Inbox");
        var planner = new SharedNotePlanner(_graph, _clock);

        var plan = planner.Plan(planner.Preview("Dune", new ParentLink(parent, TypeCatalog.IsA.Id)));

        var link = Assert.Single(plan.Changes.OfType<LinkAdded>()).Link;
        Assert.Equal((TypeCatalog.IsA.Id, parent.Id, plan.Node.Id), (link.Type, link.From, link.To));
    }

    private Node Add(string name)
    {
        var node = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, name, _clock.Now);
        _graph.Add(node);
        return node;
    }
}
