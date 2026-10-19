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
        Assert.False(form.IsTypeFixed);
        Assert.Equal(string.Empty, form.Name);
        Assert.False(form.CanAdd);
        Assert.Equal("Type its name.", form.Status);
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
        var form = CreationForm.NewNode(_graph, TypeCatalog.Note, _clock);
        form.Name = "Memo";

        Assert.True(form.IsTypeFixed);
        Assert.Same(TypeCatalog.Note, form.TypeOf(Side.Left));
        Assert.Equal(TypeCatalog.Note.Id, Assert.IsType<NodeAdded>(Assert.Single(form.Plan.Changes)).Node.Type);
        Assert.Throws<InvalidOperationException>(() => form.SetType(Side.Left, TypeCatalog.Generic));
    }

    [Fact]
    public void ANewView_StillNeedsItsDefinition()
    {
        var form = CreationForm.NewNode(_graph, TypeCatalog.View, _clock);

        form.Name = "Files";

        Assert.True(form.IsView(Side.Left));
        Assert.False(form.CanAdd);
    }

    [Fact]
    public void ANameFromTheRequest_CannotBeTyped()
    {
        var form = new CreationForm(_graph, new NodeRequest("Dune"), clock: _clock);

        Assert.False(form.IsNameEditable);
        Assert.Throws<InvalidOperationException>(() => form.Name = "Arrakis");
    }
}
