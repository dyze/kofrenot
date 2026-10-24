namespace Kofrenot.Core.Tests.Browse;

public sealed class NodeCardTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();
    private readonly Dictionary<Guid, string> _contents = [];
    private readonly List<Guid> _read = [];

    [Fact]
    public void ANote_ShowsItsLatestContent_ReadForTheCard()
    {
        var note = Add(TypeCatalog.Note.Id);
        var old = Edit(note, "old", minutes: 1, replaces: []);
        var latest = Edit(note, "- [ ] buy\n- [x] read", minutes: 2, replaces: [old]);

        var body = Assert.IsType<NoteCardBody>(Card(note).Body);

        Assert.Equal(["buy"], body.Preview.Items);
        Assert.Equal(1, body.Preview.CheckedCount);
        Assert.Equal([latest], _read);
    }

    [Fact]
    public void ANoteNeverSaved_IsEmpty_AndReadsNothing()
    {
        var body = Assert.IsType<NoteCardBody>(Card(Add(TypeCatalog.Note.Id)).Body);

        Assert.Empty(body.Preview.Lines);
        Assert.Empty(_read);
    }

    [Fact]
    public void ANoteWhoseContentHasNotArrived_SaysSo()
    {
        var note = Add(TypeCatalog.Note.Id);
        _graph.Apply(new NoteEdited(Guid.NewGuid(), Start, note.Id, []));

        Assert.IsType<MissingCardBody>(Card(note).Body);
    }

    [Fact]
    public void ADataset_ShowsItsLatestFilesKindAndSize_AndItsThumbnail()
    {
        var dataset = Add(TypeCatalog.Dataset.Id);
        var version = new DatasetEdited(Guid.NewGuid(), Start, dataset.Id, new DatasetFile("photo.png", 2048, new string('0', 64)), []);
        _graph.Apply(version);

        var card = Card(dataset);

        Assert.Equal(new FileCardBody("PNG file", 2048), card.Body);
        Assert.Equal(version.Id, card.Visual.Thumbnail);
    }

    [Fact]
    public void ADatasetWithoutFile_SaysSo() =>
        Assert.Equal(new TextCardBody("No file yet"), Card(Add(TypeCatalog.Dataset.Id)).Body);

    [Fact]
    public void AView_SaysWhatItShows()
    {
        var view = Add(TypeCatalog.View.Id);
        _graph.Apply(new ViewEdited(Guid.NewGuid(), Start, view.Id, new FlatDefinition(TypeCatalog.Note.Id), []));

        Assert.Equal(new TextCardBody("A list of every Note"), Card(view).Body);
    }

    [Fact]
    public void AnotherNode_ShowsItsNameOnly_WithItsColour()
    {
        var node = Add(TypeCatalog.Generic.Id);
        _graph.Apply(new NodeColored(Guid.NewGuid(), Start, node.Id, "teal", []));

        var card = Card(node);

        Assert.Null(card.Body);
        Assert.Equal((node, NodeColor.Teal), (card.Node, card.Visual.Color));
    }

    private NodeCard Card(Node node) => NodeCard.Of(_graph, node, version =>
    {
        _read.Add(version);
        return _contents.GetValueOrDefault(version);
    });

    private Guid Edit(Node note, string text, int minutes, Guid[] replaces)
    {
        var version = new NoteEdited(Guid.NewGuid(), Start.AddMinutes(minutes), note.Id, replaces);
        _graph.Apply(version);
        _contents[version.Id] = text;
        return version.Id;
    }

    private Node Add(string type)
    {
        var node = new Node(Guid.NewGuid(), type, type, Start);
        _graph.Add(node);
        return node;
    }
}
