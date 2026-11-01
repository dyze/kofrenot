namespace Kofrenot.Core.Tests.Input;

/// <summary>The nodes an image is given to, and the image datasets it is taken from, nothing copied.</summary>
public sealed class ImagePickerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly FieldDefinition Portrait = new("portrait", "Portrait", FieldKind.Image);
    private static readonly FieldDefinition Signature = new("signature", "Signature", FieldKind.Image);
    private static readonly FieldDefinition Born = new("born", "Born", FieldKind.Date);

    private readonly KnowledgeGraph _graph = new();
    private readonly ManualClock _clock = new() { Now = Start.AddMinutes(10) };

    public ImagePickerTests()
    {
        _graph.Apply(new NodeTypeDefined(Guid.NewGuid(), Start, "person", "Person", null, null, [Born, Portrait, Signature], []));
        _graph.Apply(new NodeTypeDefined(Guid.NewGuid(), Start, "place", "Place", null, null, [Born], []));
    }

    [Fact]
    public void ANodesImageFields_AreListed_InTheirOrder()
    {
        var bruce = Add("Bruce", "person");
        var arrakis = Add("Arrakis", "place");

        Assert.Equal(["Portrait", "Signature"], ImagePicker.FieldsOf(_graph, bruce.Id).Select(field => field.Label));
        Assert.Empty(ImagePicker.FieldsOf(_graph, arrakis.Id));
    }

    [Fact]
    public void OnlyNodesWithAnImageField_AreTargets_TheNodeShownFirst()
    {
        var bruce = Add("Bruce", "person");
        var brian = Add("Brian", "person");
        Add("Brest", "place");

        Assert.Equal([brian, bruce], ImagePicker.Targets(_graph, "br"));
        Assert.Equal([bruce, brian], ImagePicker.Targets(_graph, "br", first: bruce));
        Assert.Equal([bruce], ImagePicker.Targets(_graph, string.Empty, first: bruce));
        Assert.Empty(ImagePicker.Targets(_graph, string.Empty));
        Assert.Equal([brian], ImagePicker.Targets(_graph, "brian", first: Add("Brest harbour", "place")));
    }

    [Fact]
    public void OnlyImageDatasets_AreOffered_ByName_TheirLatestVersion()
    {
        var cover = Dataset("Cover", "cover.png");
        var latest = Version(cover, "cover-2.jpg");
        var arcane = Dataset("Arcane poster", "arcane.webp");
        Dataset("Survey", "survey.csv");
        Add("Empty", TypeCatalog.Dataset.Id);
        Dataset("Huge", "huge.png", size: DatasetType.MaxImageSize + 1);

        Assert.Equal([arcane.Node, cover.Node], ImagePicker.Images(_graph, " ").Select(choice => choice.Node));
        Assert.Equal(latest, ImagePicker.Images(_graph, "cov").Single().Image);
        Assert.Empty(ImagePicker.Images(_graph, "survey"));
    }

    [Fact]
    public void AnImageDataset_IsAFieldsImage_WithoutACopy_EvenOnceReplaced()
    {
        var bruce = Add("Bruce", "person");
        var cover = Dataset("Cover", "cover.png");
        var portrait = ImagePicker.FieldsOf(_graph, bruce.Id)[0];

        var plan = new FieldPlanner(_graph, _clock).SetImageFrom(bruce, portrait, cover.Image);
        var value = Assert.IsType<FieldSet>(Assert.Single(plan.Changes));
        _graph.Apply(value);
        Version(cover, "cover-2.png");

        var image = ImagePicker.FieldsOf(_graph, bruce.Id)[0].Image!.Image!;
        Assert.Equal((cover.Image.Id, cover.Image.File), (image.Id, image.File));
        Assert.Equal(cover.Image.Id, value.Content);
    }

    [Fact]
    public void ADatasetNotAnImage_IsRefused_AsAFileWouldBe()
    {
        var bruce = Add("Bruce", "person");
        var survey = Dataset("Survey", "survey.csv");

        var plan = new FieldPlanner(_graph, _clock).SetImageFrom(bruce, ImagePicker.FieldsOf(_graph, bruce.Id)[0], survey.Image);

        Assert.Empty(plan.Changes);
        Assert.StartsWith("“survey.csv” is not an image kofrenot shows", plan.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void AFileShared_CanBeAnImage_WhenItIsOneShownAndNotTooLarge()
    {
        Assert.True(ImagePicker.CanBeImage(new ImportFile("photo.JPG", 100, () => Stream.Null)));
        Assert.False(ImagePicker.CanBeImage(new ImportFile("survey.csv", 100, () => Stream.Null)));
        Assert.False(ImagePicker.CanBeImage(new ImportFile("huge.png", DatasetType.MaxImageSize + 1, () => Stream.Null)));
    }

    private Node Add(string name, string type)
    {
        var node = new Node(Guid.NewGuid(), type, name, Start);
        _graph.Add(node);
        return node;
    }

    private ImageChoice Dataset(string name, string file, long size = 100)
    {
        var node = Add(name, TypeCatalog.Dataset.Id);
        var version = new DatasetEdited(Guid.NewGuid(), Start, node.Id, new DatasetFile(file, size, new string('a', 64)), []);
        _graph.Apply(version);
        return new ImageChoice(node, version);
    }

    private DatasetEdited Version(ImageChoice dataset, string file)
    {
        var version = new DatasetEdited(Guid.NewGuid(), Start.AddMinutes(1), dataset.Node.Id, new DatasetFile(file, 200, new string('b', 64)), [dataset.Image.Id]);
        _graph.Apply(version);
        return version;
    }
}
