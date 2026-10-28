using System.Security.Cryptography;
using System.Text;

namespace Kofrenot.Core.Tests.Fields;

/// <summary>An image field: its kind, its value (a file, stored as a dataset version's), shown, planned and written.</summary>
public sealed class ImageFieldTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly FieldDefinition Portrait = new("portrait", "Portrait", FieldKind.Image);
    private static readonly DatasetFile Photo = new("bruce.jpg", 1200, new string('a', 64));
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(SecretBox.KeySize);

    private readonly KnowledgeGraph _graph = new();
    private readonly ManualClock _clock = new() { Now = Start.AddMinutes(10) };
    private readonly Node _bruce = new(Guid.NewGuid(), "person", "Bruce", Start);

    public ImageFieldTests()
    {
        _graph.Apply(new NodeTypeDefined(Guid.NewGuid(), Start, "person", "Person", null, null, [Portrait], []));
        _graph.Add(_bruce);
    }

    private NodeField Field => NodeFields.Of(_graph, _bruce.Id).Single();

    private FieldPlanner Planner => new(_graph, _clock);

    private FieldSet Apply(FieldPlan plan)
    {
        var value = Assert.IsType<FieldSet>(Assert.Single(plan.Changes));
        _graph.Apply(value);
        return value;
    }

    [Fact]
    public void AnImage_IsAKindOfATypesField_NotTypedInNorContent()
    {
        Assert.Contains(FieldKind.Image, FieldKinds.OfTypes);
        Assert.DoesNotContain(FieldKind.Image, FieldKinds.Small);
        Assert.Equal(FieldKind.Image, FieldKinds.Parse("image"));
        Assert.False(FieldKind.Image.IsSmall());
        Assert.False(FieldKind.Image.IsContent());
        Assert.True(FieldKind.Markdown.IsContent());
        Assert.Equal("An image is chosen, not typed in", FieldValue.Parse(FieldKind.Image, "bruce.jpg").Error);
        Assert.False(FieldValue.IsValid(FieldKind.Image, "bruce.jpg"));
        Assert.DoesNotContain(FieldKind.Image, FieldPlanner.OwnKinds);
    }

    [Fact]
    public void AnImageField_ShowsEmpty_ThenItsFile()
    {
        Assert.Equal((false, (FieldSet?)null, ""), (Field.HasValue, Field.Image, Field.Display));

        var set = Apply(Planner.SetImage(_bruce, Field, Photo));

        Assert.Equal((true, set, "bruce.jpg", (string?)null), (Field.HasValue, Field.Image, Field.Display, Field.Value));
        Assert.Equal((set.Id, Photo, _bruce.Id), (set.Image!.Id, set.Image.File, set.Image.Node));
    }

    [Fact]
    public void ANewImage_ReplacesTheCurrentOne_AndTheSameIsUnchanged()
    {
        var first = Apply(Planner.SetImage(_bruce, Field, Photo));

        Assert.Equal(FieldProblem.Unchanged, Planner.SetImage(_bruce, Field, Photo).Problem);

        var second = Apply(Planner.SetImage(_bruce, Field, Photo with { Name = "bruce-2.png" }));
        Assert.Equal([first.Id], second.Replaces);
    }

    [Fact]
    public void AnImageCleared_HasNoFile()
    {
        Apply(Planner.SetImage(_bruce, Field, Photo));

        var cleared = Apply(Planner.Clear(_bruce, Field));

        Assert.Null(cleared.File);
        Assert.Null(cleared.Image);
        Assert.False(Field.HasValue);
    }

    [Theory]
    [InlineData("bruce.svg", 10, "“bruce.svg” is not an image kofrenot shows: choose a PNG, JPEG, GIF, BMP, TIFF or WebP.")]
    [InlineData("notes.txt", 10, "“notes.txt” is not an image kofrenot shows: choose a PNG, JPEG, GIF, BMP, TIFF or WebP.")]
    [InlineData("huge.png", DatasetType.MaxImageSize + 1, "“huge.png” is too large for an image field: 64.0 MB at most.")]
    public void AFileNotShown_IsRefused(string name, long size, string why)
    {
        var plan = Planner.SetImage(_bruce, Field, new DatasetFile(name, size, Photo.Sha256));

        Assert.Equal((FieldProblem.Invalid, why), (plan.Problem, plan.Status));
    }

    [Fact]
    public void AnImage_IsForAnImageField_Only()
    {
        _graph.Apply(new NodeTypeDefined(Guid.NewGuid(), Start.AddMinutes(1), "person", "Person", null, null, [new FieldDefinition("born", "Born", FieldKind.Date)], []));
        var born = NodeFields.Of(_graph, _bruce.Id).Single(field => field.Kind == FieldKind.Date);

        Assert.Throws<ArgumentException>(() => Planner.SetImage(_bruce, born, Photo));
    }

    [Fact]
    public void AnImageKept_FromAConflict_KeepsTheFileOfItsChange()
    {
        var first = Apply(Planner.SetImage(_bruce, Field, Photo));
        var mine = first with { Id = Guid.NewGuid(), At = Start.AddMinutes(11), File = Photo with { Name = "mine.png" }, Replaces = [first.Id] };
        var theirs = first with { Id = Guid.NewGuid(), At = Start.AddMinutes(12), File = Photo with { Name = "theirs.png" }, Replaces = [first.Id] };
        _graph.Apply(mine);
        _graph.Apply(theirs);

        Assert.Equal(["mine.png", "theirs.png"], Field.Conflict);

        var kept = Apply(Planner.Keep(_bruce, Field, mine));

        Assert.Equal((mine.Id, "mine.png"), (kept.Image!.Id, kept.File!.Name));
        Assert.Equal(2, kept.Replaces.Count);
        Assert.Empty(Field.Conflict);

        var keptAgain = Planner.Keep(_bruce, Field, kept).Changes.OfType<FieldSet>().Single();
        Assert.Equal(mine.Id, keptAgain.Content);
    }

    [Fact]
    public void AConflictWithAnImageCleared_ShowsNone()
    {
        var first = Apply(Planner.SetImage(_bruce, Field, Photo));
        _graph.Apply(first with { Id = Guid.NewGuid(), File = null, Replaces = [first.Id] });
        _graph.Apply(first with { Id = Guid.NewGuid(), At = Start.AddMinutes(20), Replaces = [first.Id] });

        Assert.Equal(["(none)", "bruce.jpg"], Field.Conflict);
    }

    [Fact]
    public void AnImageWhoseFieldLeftItsType_StaysAsOwn()
    {
        Apply(Planner.SetImage(_bruce, Field, Photo));
        _graph.Apply(new NodeTypeDefined(Guid.NewGuid(), Start.AddMinutes(30), "person", "Person", null, null, [], [_graph.NodeTypes.All[0].Definitions[0].Id]));

        Assert.Equal((true, "bruce.jpg"), (Field.IsOwn, Field.Display));
    }

    [Fact]
    public void AnImagesValue_IsNotSearched()
    {
        Apply(Planner.SetImage(_bruce, Field, Photo));

        Assert.Empty(GraphSearch.Find(_graph, "bruce.jpg"));
    }

    [Fact]
    public void AnImageValue_RoundTrips_WithItsFileAndContent()
    {
        var codec = new ChangeCodec(new KeyedBox(KeyRing.Of(Key)));
        var change = new FieldSet(Guid.NewGuid(), Start, _bruce.Id, "person", "portrait", "Portrait", FieldKind.Image, null, [Guid.NewGuid()], Photo, Guid.NewGuid());

        var decoded = Assert.IsType<FieldSet>(codec.Decode(change.Id, codec.Encode(change)));

        Assert.Equal((change.Kind, change.Value, change.File, change.Content), (decoded.Kind, decoded.Value, decoded.File, decoded.Content));
    }

    private const string Node = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";
    private const string File = ",\"file\":{\"name\":\"bruce.jpg\",\"size\":1,\"sha256\":\"" + "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08" + "\"}";

    [Theory]
    [InlineData("\"kind\":\"image\",\"value\":\"bruce.jpg\"")]
    [InlineData("\"kind\":\"image\",\"content\":\"" + Node + "\"")]
    [InlineData("\"kind\":\"image\",\"content\":\"00000000-0000-0000-0000-000000000000\"" + File)]
    [InlineData("\"kind\":\"image\",\"file\":{\"name\":\"bruce.jpg\",\"size\":1}")]
    [InlineData("\"kind\":\"text\"" + File)]
    [InlineData("\"kind\":\"text\",\"value\":\"x\",\"content\":\"" + Node + "\"")]
    public void AnImageValueNotWritten_AsAnImageIs_IsRefused(string value)
    {
        var codec = new ChangeCodec(new KeyedBox(KeyRing.Of(Key)));
        var id = Guid.NewGuid();
        var json = "{\"format\":8,\"at\":\"2026-10-16T09:12:00Z\",\"op\":\"setField\",\"field\":{\"node\":\"" + Node + "\",\"owner\":\"person\",\"id\":\"portrait\",\"name\":\"Portrait\",\"replaces\":[]," + value + "}}";
        var data = new SecretBox(Key).Seal("KFC1"u8, Encoding.UTF8.GetBytes(json), id.ToByteArray());

        Assert.Throws<InvalidDataException>(() => codec.Decode(id, data));
    }
}
