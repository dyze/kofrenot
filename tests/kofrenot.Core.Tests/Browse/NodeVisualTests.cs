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
