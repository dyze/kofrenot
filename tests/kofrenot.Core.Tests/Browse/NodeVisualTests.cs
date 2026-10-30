namespace Kofrenot.Core.Tests.Browse;

public sealed class NodeVisualTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);

    private readonly KnowledgeGraph _graph = new();

    [Theory]
    [InlineData("generic", NodeIcon.Generic)]
    [InlineData("note", NodeIcon.Note)]
    [InlineData("book", NodeIcon.Generic)]
    [InlineData("dataset", NodeIcon.File)]
    [InlineData("view", NodeIcon.View)]
    public void ANodeWithoutFile_HasTheIconOfItsType(string type, NodeIcon icon) =>
        Assert.Equal(new NodeVisual(icon), NodeVisual.Of(_graph, Add(type)));

    [Theory]
    [InlineData("note")]
    [InlineData("dataset")]
    public void ANode_HasItsColour(string type)
    {
        var node = Add(type);
        _graph.Apply(new NodeColored(Guid.NewGuid(), Start, node.Id, "green", []));

        Assert.Equal(NodeColor.Green, NodeVisual.Of(_graph, node).Color);
    }

    [Fact]
    public void ADatasetWithAFile_HasItsColour()
    {
        var node = Add("dataset");
        _graph.Apply(new DatasetEdited(Guid.NewGuid(), Start, node.Id, new DatasetFile("a.txt", 1, new string('0', 64)), []));
        _graph.Apply(new NodeColored(Guid.NewGuid(), Start, node.Id, "pink", []));

        Assert.Equal(new NodeVisual(NodeIcon.Text, null, NodeColor.Pink), NodeVisual.Of(_graph, node));
    }

    [Fact]
    public void ANodeOfAUsersType_HasItsIcon_AndItsColourUnlessItHasOne()
    {
        _graph.Apply(new NodeTypeDefined(Guid.NewGuid(), Start, "person", "Person", "Contact", "blue", [], []));
        var node = Add("person");

        Assert.Equal(new NodeVisual(NodeIcon.Generic, null, NodeColor.Blue, "Contact"), NodeVisual.Of(_graph, node));

        _graph.Apply(new NodeColored(Guid.NewGuid(), Start, node.Id, "green", []));

        Assert.Equal(NodeColor.Green, NodeVisual.Of(_graph, node).Color);
    }

    [Fact]
    public void AnIconThisVersionDoesNotKnow_IsNone()
    {
        _graph.Apply(new NodeTypeDefined(Guid.NewGuid(), Start, "person", "Person", "Unicorn", null, [], []));

        Assert.Null(NodeVisual.Of(_graph, Add("person")).TypeIcon);
        Assert.Equal("Contact", TypeIcons.Find("Contact"));
        Assert.Null(TypeIcons.Find(null));
    }

    [Fact]
    public void ATypeAlone_HasItsIcon() =>
        Assert.Equal(
            [NodeIcon.Generic, NodeIcon.Note, NodeIcon.File, NodeIcon.View],
            TypeCatalog.BuiltIn.NodeTypes.Select(type => NodeVisual.OfType(type.Id).Icon));

    [Theory]
    [InlineData("data.json", NodeIcon.Code)]
    [InlineData("notes.md", NodeIcon.Text)]
    [InlineData("song.mp3", NodeIcon.Audio)]
    [InlineData("clip.mp4", NodeIcon.Video)]
    [InlineData("backup.zip", NodeIcon.Archive)]
    [InlineData("paper.pdf", NodeIcon.Pdf)]
    [InlineData("drawing.svg", NodeIcon.Image)]
    [InlineData("README", NodeIcon.File)]
    public void ADataset_HasTheIconOfItsLatestFile_AndNoThumbnailWhenNotADecodedImage(string name, NodeIcon icon)
    {
        var dataset = Add("dataset");
        Import(dataset, "photo.png");
        Import(dataset, name, minutes: 1);

        Assert.Equal(new NodeVisual(icon), NodeVisual.Of(_graph, dataset));
    }

    [Fact]
    public void AnImageDataset_HasTheThumbnailOfItsLatestVersion()
    {
        var dataset = Add("dataset");
        Import(dataset, "old.png");
        var latest = Import(dataset, "photo.png", minutes: 1);

        Assert.Equal(new NodeVisual(NodeIcon.Image, latest.Id), NodeVisual.Of(_graph, dataset));
    }

    [Fact]
    public void AnImageTooLarge_HasNoThumbnail()
    {
        var dataset = Add("dataset");
        Import(dataset, "huge.png", size: DatasetType.MaxImageSize + 1);

        Assert.Equal(new NodeVisual(NodeIcon.Image), NodeVisual.Of(_graph, dataset));
    }

    [Fact]
    public void SearchAndLinkRows_ShowTheirNodesVisual()
    {
        var dataset = Add("dataset", "Photo");
        var version = Import(dataset, "photo.png");
        var album = Add("generic", "Album");
        _graph.Add(new Link(Guid.NewGuid(), TypeCatalog.Contains.Id, album.Id, dataset.Id, Start));

        var input = new InputPresenter();
        input.Update(_graph, "photo");
        var browser = new NodeBrowser();
        browser.Open(_graph, album);

        Assert.Equal(new NodeVisual(NodeIcon.Image, version.Id), input.Rows.Single(row => row.Node == dataset && !row.IsLink).Visual);
        Assert.Equal(new NodeVisual(NodeIcon.Image, version.Id), Assert.Single(browser.Details!.Links).Visual);
    }

    [Fact]
    public void ANodeWithImageFields_HasTheThumbnailOfTheFirstInTheOrderOfItsFields()
    {
        _graph.Apply(new NodeTypeDefined(Guid.NewGuid(), Start, "person", "Person", "Contact", null,
            [new FieldDefinition("portrait", "Portrait", FieldKind.Image), new FieldDefinition("badge", "Badge", FieldKind.Image)], []));
        var bruce = Add("person");
        SetImage(bruce, "person", "badge", minutes: 0);
        var portrait = SetImage(bruce, "person", "portrait", minutes: 1);

        Assert.Equal(new NodeVisual(NodeIcon.Generic, portrait.Id, NodeColor.None, "Contact"), NodeVisual.Of(_graph, bruce));
    }

    [Fact]
    public void ANodeWithoutAnImage_OrWithItsImageCleared_HasNoThumbnail()
    {
        var node = Add("generic");
        _graph.Apply(new FieldSet(Guid.NewGuid(), Start, node.Id, FieldSet.Own, "born", "Born", FieldKind.Text, "1965", []));

        Assert.Null(NodeVisual.Of(_graph, node).Thumbnail);

        var image = SetImage(node, FieldSet.Own, "photo");
        _graph.Apply(new FieldSet(Guid.NewGuid(), Start.AddMinutes(5), node.Id, FieldSet.Own, "photo", "Photo", FieldKind.Image, null, [image.Id]));

        Assert.Null(NodeVisual.Of(_graph, node).Thumbnail);
    }

    [Fact]
    public void ADataset_ShowsItsOwnImage_ElseItsImageField()
    {
        var photo = Add("dataset");
        var version = Import(photo, "photo.png");
        SetImage(photo, FieldSet.Own, "cover");
        var paper = Add("dataset");
        Import(paper, "paper.pdf");
        var cover = SetImage(paper, FieldSet.Own, "cover");

        Assert.Equal(version.Id, NodeVisual.Of(_graph, photo).Thumbnail);
        Assert.Equal(new NodeVisual(NodeIcon.Pdf, cover.Id), NodeVisual.Of(_graph, paper));
    }

    private FieldSet SetImage(Node node, string owner, string field, int minutes = 0)
    {
        var image = new FieldSet(Guid.NewGuid(), Start.AddMinutes(minutes), node.Id, owner, field, field, FieldKind.Image, null, [], new DatasetFile($"{field}.png", 10, new string('b', 64)));
        _graph.Apply(image);
        return image;
    }

    private Node Add(string type, string? name = null)
    {
        var node = new Node(Guid.NewGuid(), type, name ?? type, Start);
        _graph.Add(node);
        return node;
    }

    private DatasetEdited Import(Node dataset, string name, int minutes = 0, long size = 10)
    {
        var version = new DatasetEdited(Guid.NewGuid(), Start.AddMinutes(minutes), dataset.Id, new DatasetFile(name, size, new string('a', 64)), _graph.DatasetVersionsOf(dataset.Id).Select(current => current.Id).ToList());
        _graph.Apply(version);
        return version;
    }
}
