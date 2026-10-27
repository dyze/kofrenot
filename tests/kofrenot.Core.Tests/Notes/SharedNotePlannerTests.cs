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

        Assert.Equal(("Dune", TypeCatalog.Note.Id, _clock.Now), (plan.Node.Node.Name, plan.Node.Node.Type, plan.Node.Node.Created));
        Assert.Equal((plan.Node.Node.Id, "Dune\ntext"), (plan.Note.Version.Node, plan.Note.Content));
        Assert.Empty(plan.Note.Version.Replaces);
    }
}
