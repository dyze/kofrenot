namespace Kofrenot.Core.Tests.Input;

public sealed class CreationNewNodeTests
{
    private readonly ManualClock _clock = new();
    private readonly KnowledgeGraph _graph = new();

    [Fact]
    public void ANewNode_StartsWithoutName_AndCannotBeAdded()
    {
        var form = CreationForm.NewNode(_graph, clock: _clock);

        Assert.True(form.IsNameEditable);
        Assert.True(form.StartsOnName);
        Assert.False(form.IsTypeFixed);
        Assert.Equal(string.Empty, form.Name);
        Assert.False(form.CanAdd);
        Assert.Equal("Type its name.", form.Status);
    }

    [Fact]
    public void ANameGiven_IsTypedAlready_AndCanBeChanged()
    {
        var form = CreationForm.NewNode(_graph, TypeCatalog.Dataset, name: " report-2026.pdf ", clock: _clock);

        Assert.Equal("report-2026.pdf", form.Name);
        Assert.True(form.CanAdd);
        Assert.Equal("report-2026.pdf", Assert.IsType<NodeAdded>(Assert.Single(form.Plan.Changes)).Node.Name);

        form.Name = "Report";

        Assert.Equal("Report", form.Name);
    }

    [Fact]
    public void ATypedName_IsTrimmed_AndPlansTheNode()
    {
        var form = CreationForm.NewNode(_graph, clock: _clock);

        form.Name = "  Dune ";

        Assert.Equal("Dune", form.Name);
        Assert.True(form.CanAdd);
        Assert.Equal("Adds the node “Dune”.", form.Status);
        Assert.Equal("Dune", Assert.IsType<NodeAdded>(Assert.Single(form.Plan.Changes)).Node.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyName_CannotBeAdded(string name)
    {
        var form = CreationForm.NewNode(_graph, clock: _clock);
        form.Name = "Dune";

        form.Name = name;

        Assert.False(form.CanAdd);
        Assert.Equal("Type its name.", form.Status);
    }

    [Fact]
    public void ANameThatExists_IsWarnedAbout()
    {
        _graph.Add(new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Dune", _clock.Now));
        var form = CreationForm.NewNode(_graph, clock: _clock);

        form.Name = "dune";

        Assert.NotNull(form.Warning);
        Assert.Single(form.Existing);
    }

    [Fact]
    public void AFixedType_IsTheNodesType_AndCannotChange()
    {
        var form = CreationForm.NewNode(_graph, TypeCatalog.Note, clock: _clock);
        form.Name = "Memo";

        Assert.True(form.IsTypeFixed);
        Assert.Same(TypeCatalog.Note, form.TypeOf(Side.Left));
        Assert.Equal(TypeCatalog.Note.Id, Assert.IsType<NodeAdded>(Assert.Single(form.Plan.Changes)).Node.Type);
        Assert.Throws<InvalidOperationException>(() => form.SetType(Side.Left, TypeCatalog.Generic));
    }

    [Fact]
    public void ANewView_StillNeedsItsDefinition()
    {
        var form = CreationForm.NewNode(_graph, TypeCatalog.View, clock: _clock);

        form.Name = "Files";

        Assert.True(form.IsView(Side.Left));
        Assert.False(form.CanAdd);
    }

    [Fact]
    public void ANameFromTheRequest_CanBeRetyped()
    {
        var form = new CreationForm(_graph, new NodeRequest("Dune"), clock: _clock);

        Assert.True(form.IsNameEditable);
        Assert.False(form.StartsOnName);

        form.Name = " Arrakis ";

        Assert.Equal("Arrakis", form.Name);
        Assert.Equal("Arrakis", Assert.IsType<NodeAdded>(Assert.Single(form.Plan.Changes)).Node.Name);
    }

    [Fact]
    public void ALinksNames_CannotBeTyped()
    {
        var form = new CreationForm(_graph, new LinkRequest("Dune", TypeCatalog.RelatesTo.Forward, "Arrakis"), clock: _clock);

        Assert.False(form.IsNameEditable);
        Assert.Throws<InvalidOperationException>(() => form.Name = "Caladan");
    }

    [Fact]
    public void ANodeUnderAParent_IsLinkedFromIt()
    {
        var projects = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Projects", _clock.Now);
        _graph.Add(projects);
        var form = CreationForm.NewNode(_graph, under: new ParentLink(projects, TypeCatalog.Contains.Id), clock: _clock);
        form.Name = "kofrenot";

        var added = Assert.Single(form.Plan.NewNodes);
        var link = Assert.Single(form.LinksToSave()).Link;

        Assert.Equal("Add a node: “Projects” contains …", form.Title);
        Assert.Equal((TypeCatalog.Contains.Id, projects.Id, added.Id), (link.Type, link.From, link.To));
    }

    [Fact]
    public void ANodeLinkedReversed_IsLinkedToTheNodeGiven_AndTheTitleReadsTheLinkFromIt()
    {
        var dune = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Dune", _clock.Now);
        _graph.Add(dune);
        var form = CreationForm.NewNode(_graph, under: new ParentLink(dune, TypeCatalog.Contains.Id, IsReverse: true), clock: _clock);
        form.Name = "Books";

        var added = Assert.Single(form.Plan.NewNodes);
        var link = Assert.Single(form.LinksToSave()).Link;

        Assert.Equal($"Add a node: “Dune” {TypeCatalog.Contains.Reverse!.Name} …", form.Title);
        Assert.Equal((added.Id, dune.Id), (link.From, link.To));
    }

    [Fact]
    public void ALinkOfATypeNotKnown_OrReversedWithoutAReverseName_SaysUnder()
    {
        var dune = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Dune", _clock.Now);
        _graph.Add(dune);

        Assert.Equal("Add a node under “Dune”", CreationForm.NewNode(_graph, under: new ParentLink(dune, "unknown"), clock: _clock).Title);
        Assert.Equal("Add a node under “Dune”", CreationForm.NewNode(_graph, under: new ParentLink(dune, TypeCatalog.RelatesTo.Id, IsReverse: true), clock: _clock).Title);
    }

    [Fact]
    public void WithoutParent_NoLinkIsAdded()
    {
        var form = CreationForm.NewNode(_graph, clock: _clock);
        form.Name = "Dune";

        Assert.Equal("Add a node", form.Title);
        Assert.Empty(form.LinksToSave());
    }
}
