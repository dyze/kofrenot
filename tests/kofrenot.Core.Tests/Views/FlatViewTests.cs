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

    [Fact]
    public void Find_GivesTheMatchingNodes_InTheOrderShown()
    {
        var shopping = Add("Shopping list", TypeCatalog.Note.Id);
        var ideas = Add("Ideas", TypeCatalog.Note.Id);
        var lists = Add("Lists to do", TypeCatalog.Note.Id);
        Add("List of books", TypeCatalog.Generic.Id);

        var view = new FlatView(_graph, new FlatDefinition(TypeCatalog.Note.Id));

        Assert.Equal([lists, shopping], view.Find("list"));
        Assert.Empty(view.Find(""));
        Assert.DoesNotContain(ideas, view.Find("list"));
    }

    private Node Add(string name, string type, int minutes = 0)
    {
        var node = new Node(Guid.NewGuid(), type, name, Start.AddMinutes(minutes));
        _graph.Apply(new NodeAdded(Guid.NewGuid(), node.Created, node));
        return node;
    }
}
