namespace Kofrenot.Core.Tests.Browse;

public sealed class NodeFactsTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();

    [Fact]
    public void ANewNode_HasItsTypesName_NoSize_AndItsCreation()
    {
        var node = Add(TypeCatalog.Generic.Id);

        Assert.Equal(new NodeFacts("Generic", null, Start), NodeFacts.Of(_graph, node));
    }

    [Fact]
    public void ARename_ChangesIt()
    {
        var node = Add(TypeCatalog.Generic.Id);
        _graph.Apply(new NodeRenamed(Guid.NewGuid(), Start.AddMinutes(5), node.Id, "Other", _graph.NamesOf(node.Id).Select(name => name.Id).ToList()));

        Assert.Equal(Start.AddMinutes(5), NodeFacts.Of(_graph, node).Modified);
    }

    [Fact]
    public void ARetype_ChangesIt_AndItsType()
    {
        var node = Add(TypeCatalog.Generic.Id);
        _graph.Apply(new NodeRetyped(Guid.NewGuid(), Start.AddMinutes(4), node.Id, TypeCatalog.Note.Id, [node.Id]));

        Assert.Equal(new NodeFacts("Note", null, Start.AddMinutes(4)), NodeFacts.Of(_graph, _graph.FindNode(node.Id)!));
    }

    [Fact]
    public void ANote_ChangesWithItsContent()
    {
        var note = Add(TypeCatalog.Note.Id);
        _graph.Apply(new NoteEdited(Guid.NewGuid(), Start.AddMinutes(3), note.Id, []));

        Assert.Equal(new NodeFacts("Note", null, Start.AddMinutes(3)), NodeFacts.Of(_graph, note));
    }

    [Fact]
    public void ADataset_HasItsLatestFilesKindAndSize()
    {
        var dataset = Add(TypeCatalog.Dataset.Id);
        Import(dataset, "old.txt", 10, minutes: 1);
        Import(dataset, "photo.png", 2048, minutes: 2);

        Assert.Equal(new NodeFacts("PNG file", 2048, Start.AddMinutes(2)), NodeFacts.Of(_graph, dataset));
    }

    [Fact]
    public void ADatasetWithoutFile_IsADataset()
    {
        var dataset = Add(TypeCatalog.Dataset.Id);

        Assert.Equal(new NodeFacts("Dataset", null, Start), NodeFacts.Of(_graph, dataset));
    }

    [Fact]
    public void InAConflict_TheLatestVersionCounts()
    {
        var dataset = Add(TypeCatalog.Dataset.Id);
        Import(dataset, "a.png", 1, minutes: 7, replaces: []);
        Import(dataset, "b.json", 2, minutes: 4, replaces: []);

        Assert.Equal(Start.AddMinutes(7), NodeFacts.Of(_graph, dataset).Modified);
    }

    [Fact]
    public void AView_ChangesWithItsDefinition()
    {
        var view = Add(TypeCatalog.View.Id);
        _graph.Apply(new ViewEdited(Guid.NewGuid(), Start.AddMinutes(9), view.Id, new FlatDefinition(TypeCatalog.Note.Id), []));

        Assert.Equal(new NodeFacts("View", null, Start.AddMinutes(9)), NodeFacts.Of(_graph, view));
    }

    private Node Add(string type)
    {
        var node = new Node(Guid.NewGuid(), type, type, Start);
        _graph.Add(node);
        return node;
    }

    private void Import(Node dataset, string name, long size, int minutes, IReadOnlyList<Guid>? replaces = null) =>
        _graph.Apply(new DatasetEdited(
            Guid.NewGuid(),
            Start.AddMinutes(minutes),
            dataset.Id,
            new DatasetFile(name, size, new string('a', 64)),
            replaces ?? _graph.DatasetVersionsOf(dataset.Id).Select(current => current.Id).ToList()));
}
