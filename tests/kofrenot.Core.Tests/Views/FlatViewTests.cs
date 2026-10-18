namespace Kofrenot.Core.Tests.Views;

public sealed class FlatViewTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();

    [Fact]
    public void Nodes_AreThoseOfItsType_ByName_HomonymsOldestFirst()
    {
        var newer = Add("Shopping", TypeCatalog.Note.Id, minutes: 5);
        var ideas = Add("ideas", TypeCatalog.Note.Id);
        var older = Add("shopping", TypeCatalog.Note.Id, minutes: 1);
        Add("Dune", TypeCatalog.Generic.Id);

        var view = new FlatView(_graph, new FlatDefinition(TypeCatalog.Note.Id));

        Assert.Equal([ideas, older, newer], view.Nodes);
    }

    [Fact]
    public void ANodeAddedOrRemovedLater_Shows()
    {
        var view = new FlatView(_graph, new FlatDefinition(TypeCatalog.Note.Id));
        var ideas = Add("Ideas", TypeCatalog.Note.Id);

        Assert.Equal([ideas], view.Nodes);

        _graph.Apply(new ChangesRemoved(Guid.NewGuid(), Start, [ideas.Id]));
        Assert.Empty(view.Nodes);
    }

    private Node Add(string name, string type, int minutes = 0)
    {
        var node = new Node(Guid.NewGuid(), type, name, Start.AddMinutes(minutes));
        _graph.Apply(new NodeAdded(Guid.NewGuid(), node.Created, node));
        return node;
    }
}
