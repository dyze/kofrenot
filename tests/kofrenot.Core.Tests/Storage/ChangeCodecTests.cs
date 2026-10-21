using System.Security.Cryptography;
using System.Text;

namespace Kofrenot.Core.Tests.Storage;

public sealed class ChangeCodecTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 16, 9, 12, 0, TimeSpan.Zero);

    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(SecretBox.KeySize);

    // Writes change files as kofrenot did before format 6 ("KFC1", no epoch): still read, with epoch 0.
    private readonly SecretBox _box = new(Key);
    private readonly KeyedBox _keyed = new(KeyRing.Of(Key));
    private readonly ChangeCodec _codec;

    public ChangeCodecTests() => _codec = new ChangeCodec(_keyed);

    [Fact]
    public void AnAddedNode_RoundTrips()
    {
        var change = new NodeAdded(Guid.NewGuid(), At, new Node(Guid.NewGuid(), "generic", "Dune", At));

        Assert.Equal(change, _codec.Decode(change.Id, _codec.Encode(change)));
    }

    [Fact]
    public void AnAddedLink_RoundTrips()
    {
        var change = new LinkAdded(Guid.NewGuid(), At, new Link(Guid.NewGuid(), "relates-to", Guid.NewGuid(), Guid.NewGuid(), At));

        Assert.Equal(change, _codec.Decode(change.Id, _codec.Encode(change)));
    }

    [Fact]
    public void AnAlias_RoundTrips()
    {
        var change = new AliasAdded(Guid.NewGuid(), At, "contains", IsReverse: true, "is in");

        Assert.Equal(change, _codec.Decode(change.Id, _codec.Encode(change)));
    }

    [Fact]
    public void ARemoval_RoundTrips()
    {
        var change = new ChangesRemoved(Guid.NewGuid(), At, [Guid.NewGuid(), Guid.NewGuid()]);

        var decoded = Assert.IsType<ChangesRemoved>(_codec.Decode(change.Id, _codec.Encode(change)));

        Assert.Equal((change.Id, change.At), (decoded.Id, decoded.At));
        Assert.Equal(change.Removes, decoded.Removes);
    }

    [Fact]
    public void AVaultName_RoundTrips()
    {
        var change = new VaultNamed(Guid.NewGuid(), At, "Personal", [Guid.NewGuid()]);

        var decoded = Assert.IsType<VaultNamed>(_codec.Decode(change.Id, _codec.Encode(change)));

        Assert.Equal((change.Id, change.At, change.Name), (decoded.Id, decoded.At, decoded.Name));
        Assert.Equal(change.Replaces, decoded.Replaces);
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"vault\":{\"replaces\":[]}")]
    [InlineData(",\"vault\":{\"name\":\" \",\"replaces\":[]}")]
    [InlineData(",\"vault\":{\"name\":\"Personal\"}")]
    public void AnIncompleteVaultName_IsRefused(string vault)
    {
        var id = Guid.NewGuid();
        var json = "{\"format\":5,\"op\":\"nameVault\",\"at\":\"2026-10-16T09:12:00Z\"" + vault + "}";
        var data = _box.Seal("KFC1"u8, Encoding.UTF8.GetBytes(json), id.ToByteArray());

        Assert.Throws<InvalidDataException>(() => _codec.Decode(id, data));
    }

    [Fact]
    public void ARetype_RoundTrips()
    {
        var change = new NodeRetyped(Guid.NewGuid(), At, Guid.NewGuid(), "note", [Guid.NewGuid()]);

        var decoded = Assert.IsType<NodeRetyped>(_codec.Decode(change.Id, _codec.Encode(change)));

        Assert.Equal((change.Id, change.At, change.Node, change.Type), (decoded.Id, decoded.At, decoded.Node, decoded.Type));
        Assert.Equal(change.Replaces, decoded.Replaces);
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"retype\":{\"type\":\"note\",\"replaces\":[\"" + Node + "\"]}")]
    [InlineData(",\"retype\":{\"node\":\"" + Node + "\",\"replaces\":[\"" + Node + "\"]}")]
    [InlineData(",\"retype\":{\"node\":\"" + Node + "\",\"type\":\"\",\"replaces\":[\"" + Node + "\"]}")]
    [InlineData(",\"retype\":{\"node\":\"" + Node + "\",\"type\":\"note\",\"replaces\":[]}")]
    [InlineData(",\"retype\":{\"node\":\"" + Node + "\",\"type\":\"note\"}")]
    public void AnIncompleteRetype_IsRefused(string retype)
    {
        var id = Guid.NewGuid();
        var json = "{\"format\":5,\"op\":\"retypeNode\",\"at\":\"2026-10-16T09:12:00Z\"" + retype + "}";
        var data = _box.Seal("KFC1"u8, Encoding.UTF8.GetBytes(json), id.ToByteArray());

        Assert.Throws<InvalidDataException>(() => _codec.Decode(id, data));
    }

    [Fact]
    public void APin_RoundTrips()
    {
        var change = new NodePinned(Guid.NewGuid(), At, Guid.NewGuid());

        Assert.Equal(change, _codec.Decode(change.Id, _codec.Encode(change)));
    }

    [Fact]
    public void ANoteVersion_RoundTrips()
    {
        var change = new NoteEdited(Guid.NewGuid(), At, Guid.NewGuid(), [Guid.NewGuid()]);

        var decoded = Assert.IsType<NoteEdited>(_codec.Decode(change.Id, _codec.Encode(change)));

        Assert.Equal((change.Id, change.At, change.Node), (decoded.Id, decoded.At, decoded.Node));
        Assert.Equal(change.Replaces, decoded.Replaces);
    }

    [Fact]
    public void ADatasetVersion_RoundTrips_WithItsFile()
    {
        var change = new DatasetEdited(Guid.NewGuid(), At, Guid.NewGuid(), new DatasetFile("survey.csv", 52430, new string('0', 64)), [Guid.NewGuid()]);

        var decoded = Assert.IsType<DatasetEdited>(_codec.Decode(change.Id, _codec.Encode(change)));

        Assert.Equal((change.Id, change.At, change.Node, change.File), (decoded.Id, decoded.At, decoded.Node, decoded.File));
        Assert.Equal(change.Replaces, decoded.Replaces);
    }

    [Fact]
    public void ADatasetVersion_HasTheDocumentedShape()
    {
        var change = new DatasetEdited(Guid.NewGuid(), At, Guid.NewGuid(), new DatasetFile("survey.csv", 52430, new string('9', 64)), []);

        var json = Encoding.UTF8.GetString(_keyed.Open(SealKind.Change, _codec.Encode(change), change.Id.ToByteArray()));

        Assert.StartsWith($"{{\"format\":{VaultFormat.Current},\"op\":\"editDataset\",", json);
        Assert.Contains($"\"dataset\":{{\"node\":\"{change.Node}\",\"file\":{{\"name\":\"survey.csv\",\"size\":52430,\"sha256\":\"{new string('9', 64)}\"}},\"replaces\":[]}}", json);
    }

    [Theory]
    [InlineData("{\"format\":3,\"op\":\"editDataset\",\"at\":\"2026-10-16T09:12:00Z\"}")]
    [InlineData("{\"format\":3,\"op\":\"editDataset\",\"at\":\"2026-10-16T09:12:00Z\",\"dataset\":{\"node\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"replaces\":[]}}")]
    [InlineData("{\"format\":3,\"op\":\"editDataset\",\"at\":\"2026-10-16T09:12:00Z\",\"dataset\":{\"node\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"file\":{\"name\":\"a\",\"size\":1,\"sha256\":\"" + Hash + "\"}}}")]
    [InlineData("{\"format\":3,\"op\":\"editDataset\",\"at\":\"2026-10-16T09:12:00Z\",\"dataset\":{\"file\":{\"name\":\"a\",\"size\":1,\"sha256\":\"" + Hash + "\"},\"replaces\":[]}}")]
    [InlineData("{\"format\":3,\"op\":\"editDataset\",\"at\":\"2026-10-16T09:12:00Z\",\"dataset\":{\"node\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"file\":{\"size\":1,\"sha256\":\"" + Hash + "\"},\"replaces\":[]}}")]
    [InlineData("{\"format\":3,\"op\":\"editDataset\",\"at\":\"2026-10-16T09:12:00Z\",\"dataset\":{\"node\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"file\":{\"name\":\" \",\"size\":1,\"sha256\":\"" + Hash + "\"},\"replaces\":[]}}")]
    [InlineData("{\"format\":3,\"op\":\"editDataset\",\"at\":\"2026-10-16T09:12:00Z\",\"dataset\":{\"node\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"file\":{\"name\":\"a\",\"size\":-1,\"sha256\":\"" + Hash + "\"},\"replaces\":[]}}")]
    [InlineData("{\"format\":3,\"op\":\"editDataset\",\"at\":\"2026-10-16T09:12:00Z\",\"dataset\":{\"node\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"file\":{\"name\":\"a\",\"size\":1},\"replaces\":[]}}")]
    [InlineData("{\"format\":3,\"op\":\"editDataset\",\"at\":\"2026-10-16T09:12:00Z\",\"dataset\":{\"node\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"file\":{\"name\":\"a\",\"size\":1,\"sha256\":\"abc\"},\"replaces\":[]}}")]
    [InlineData("{\"format\":3,\"op\":\"editDataset\",\"at\":\"2026-10-16T09:12:00Z\",\"dataset\":{\"node\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"file\":{\"name\":\"a\",\"size\":1,\"sha256\":\"" + UpperHash + "\"},\"replaces\":[]}}")]
    public void AnIncompleteDatasetVersion_IsRefused(string json)
    {
        var id = Guid.NewGuid();
        var data = _box.Seal("KFC1"u8, Encoding.UTF8.GetBytes(json), id.ToByteArray());

        Assert.Throws<InvalidDataException>(() => _codec.Decode(id, data));
    }

    public static TheoryData<ViewDefinition> ViewDefinitions => [new TreeDefinition(Guid.NewGuid(), "contains"), new FlatDefinition("note")];

    [Theory]
    [MemberData(nameof(ViewDefinitions))]
    public void AViewVersion_RoundTrips_WithItsDefinition(ViewDefinition definition)
    {
        var change = new ViewEdited(Guid.NewGuid(), At, Guid.NewGuid(), definition, [Guid.NewGuid()]);

        var decoded = Assert.IsType<ViewEdited>(_codec.Decode(change.Id, _codec.Encode(change)));

        Assert.Equal((change.Id, change.At, change.Node, change.Definition), (decoded.Id, decoded.At, decoded.Node, decoded.Definition));
        Assert.Equal(change.Replaces, decoded.Replaces);
    }

    [Fact]
    public void AViewVersion_HasTheDocumentedShape()
    {
        var root = Guid.NewGuid();
        var change = new ViewEdited(Guid.NewGuid(), At, Guid.NewGuid(), new TreeDefinition(root, "contains"), []);

        var json = Encoding.UTF8.GetString(_keyed.Open(SealKind.Change, _codec.Encode(change), change.Id.ToByteArray()));

        Assert.Contains($"\"op\":\"editView\",", json);
        Assert.Contains($"\"view\":{{\"node\":\"{change.Node}\",\"display\":\"tree\",\"query\":{{\"root\":\"{root}\",\"linkType\":\"contains\"}},\"replaces\":[]}}", json);
    }

    [Theory]
    [InlineData("{\"node\":\"" + Node + "\",\"display\":\"tree\",\"query\":{\"root\":\"" + Node + "\",\"linkType\":\"contains\"}}")]
    [InlineData("{\"display\":\"tree\",\"query\":{\"root\":\"" + Node + "\",\"linkType\":\"contains\"},\"replaces\":[]}")]
    [InlineData("{\"node\":\"" + Node + "\",\"display\":\"tree\",\"replaces\":[]}")]
    [InlineData("{\"node\":\"" + Node + "\",\"display\":\"tree\",\"query\":{\"linkType\":\"contains\"},\"replaces\":[]}")]
    [InlineData("{\"node\":\"" + Node + "\",\"display\":\"tree\",\"query\":{\"root\":\"00000000-0000-0000-0000-000000000000\",\"linkType\":\"contains\"},\"replaces\":[]}")]
    [InlineData("{\"node\":\"" + Node + "\",\"display\":\"tree\",\"query\":{\"root\":\"" + Node + "\",\"linkType\":\"\"},\"replaces\":[]}")]
    [InlineData("{\"node\":\"" + Node + "\",\"display\":\"tree\",\"query\":{\"root\":\"" + Node + "\"},\"replaces\":[]}")]
    [InlineData("{\"node\":\"" + Node + "\",\"display\":\"flat\",\"query\":{\"nodeType\":\"\"},\"replaces\":[]}")]
    [InlineData("{\"node\":\"" + Node + "\",\"display\":\"flat\",\"query\":{\"root\":\"" + Node + "\"},\"replaces\":[]}")]
    [InlineData("{\"node\":\"" + Node + "\",\"display\":\"grid\",\"query\":{\"nodeType\":\"note\"},\"replaces\":[]}")]
    public void AnIncompleteViewVersion_IsRefused(string view)
    {
        var id = Guid.NewGuid();
        var json = "{\"format\":4,\"op\":\"editView\",\"at\":\"2026-10-16T09:12:00Z\",\"view\":" + view + "}";
        var data = _box.Seal("KFC1"u8, Encoding.UTF8.GetBytes(json), id.ToByteArray());

        Assert.Throws<InvalidDataException>(() => _codec.Decode(id, data));
    }

    [Fact]
    public void AnUnknownViewDefinition_IsNotWritten() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _codec.Encode(new ViewEdited(Guid.NewGuid(), At, Guid.NewGuid(), new OtherDefinition(), [])));

    private sealed record OtherDefinition : ViewDefinition;

    private const string Node = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";

    private const string Hash ="9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";
    private const string UpperHash = "9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08";

    [Theory]
    [InlineData("{\"format\":2,\"op\":\"editNote\",\"at\":\"2026-10-16T09:12:00Z\"}")]
    [InlineData("{\"format\":2,\"op\":\"editNote\",\"at\":\"2026-10-16T09:12:00Z\",\"note\":{\"replaces\":[]}}")]
    [InlineData("{\"format\":2,\"op\":\"editNote\",\"at\":\"2026-10-16T09:12:00Z\",\"note\":{\"node\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\"}}")]
    [InlineData("{\"format\":2,\"op\":\"pinNode\",\"at\":\"2026-10-16T09:12:00Z\"}")]
    [InlineData("{\"format\":2,\"op\":\"pinNode\",\"at\":\"2026-10-16T09:12:00Z\",\"pin\":{}}")]
    [InlineData("{\"format\":2,\"op\":\"addAlias\",\"at\":\"2026-10-16T09:12:00Z\"}")]
    [InlineData("{\"format\":2,\"op\":\"addAlias\",\"at\":\"2026-10-16T09:12:00Z\",\"alias\":{\"phrase\":\"is in\"}}")]
    [InlineData("{\"format\":2,\"op\":\"addAlias\",\"at\":\"2026-10-16T09:12:00Z\",\"alias\":{\"linkType\":\"contains\",\"phrase\":\" \"}}")]
    [InlineData("{\"format\":2,\"op\":\"remove\",\"at\":\"2026-10-16T09:12:00Z\"}")]
    [InlineData("{\"format\":2,\"op\":\"remove\",\"at\":\"2026-10-16T09:12:00Z\",\"removes\":[]}")]
    public void AnIncompleteAliasOrRemoval_IsRefused(string json)
    {
        var id = Guid.NewGuid();
        var data = _box.Seal("KFC1"u8, Encoding.UTF8.GetBytes(json), id.ToByteArray());

        Assert.Throws<InvalidDataException>(() => _codec.Decode(id, data));
    }

    [Fact]
    public void ATypeUnknownToThisVersion_IsKept()
    {
        var change = new NodeAdded(Guid.NewGuid(), At, new Node(Guid.NewGuid(), "book", "Dune", At));

        var decoded = Assert.IsType<NodeAdded>(_codec.Decode(change.Id, _codec.Encode(change)));
        Assert.Equal("book", decoded.Node.Type);
    }

    [Fact]
    public void TheJson_HasTheDocumentedShape()
    {
        var id = Guid.NewGuid();
        var change = new NodeAdded(id, At, new Node(Guid.NewGuid(), "generic", "Dune", At));

        var json = Encoding.UTF8.GetString(_keyed.Open(SealKind.Change, _codec.Encode(change), id.ToByteArray()));

        Assert.StartsWith($"{{\"format\":{VaultFormat.Current},\"op\":\"addNode\",\"at\":", json);
        Assert.Contains("\"name\":\"Dune\"", json);
        Assert.DoesNotContain("link", json);
    }

    [Fact]
    public void AFileUnderAnotherId_IsRefused()
    {
        var change = new NodeAdded(Guid.NewGuid(), At, new Node(Guid.NewGuid(), "generic", "Dune", At));

        Assert.Throws<InvalidDataException>(() => _codec.Decode(Guid.NewGuid(), _codec.Encode(change)));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("null")]
    [InlineData("{\"format\":0,\"op\":\"addNode\",\"at\":\"2026-10-16T09:12:00Z\"}")]
    [InlineData("{\"format\":1,\"op\":\"rename\",\"at\":\"2026-10-16T09:12:00Z\"}")]
    [InlineData("{\"format\":1,\"op\":\"addNode\",\"at\":\"2026-10-16T09:12:00Z\"}")]
    [InlineData("{\"format\":1,\"op\":\"addNode\",\"at\":\"2026-10-16T09:12:00Z\",\"node\":{\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"type\":\"generic\",\"name\":\" \"}}")]
    [InlineData("{\"format\":1,\"op\":\"addNode\",\"at\":\"2026-10-16T09:12:00Z\",\"node\":{\"type\":\"generic\",\"name\":\"Dune\"}}")]
    [InlineData("{\"format\":1,\"op\":\"addNode\",\"at\":\"2026-10-16T09:12:00Z\",\"node\":{\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"name\":\"Dune\"}}")]
    [InlineData("{\"format\":1,\"op\":\"addLink\",\"at\":\"2026-10-16T09:12:00Z\",\"link\":{\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"type\":\"relates-to\",\"from\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3302\"}}")]
    [InlineData("{\"format\":1,\"op\":\"addLink\",\"at\":\"2026-10-16T09:12:00Z\",\"link\":{\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"type\":\"relates-to\",\"to\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3302\"}}")]
    [InlineData("{\"format\":1,\"op\":\"addLink\",\"at\":\"2026-10-16T09:12:00Z\",\"link\":{\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"from\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3302\",\"to\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3303\"}}")]
    [InlineData("{\"format\":1,\"op\":\"addLink\",\"at\":\"2026-10-16T09:12:00Z\",\"link\":{\"type\":\"relates-to\",\"from\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3302\",\"to\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3303\"}}")]
    public void InvalidContent_IsRefused(string json)
    {
        var id = Guid.NewGuid();
        var data = _box.Seal("KFC1"u8, Encoding.UTF8.GetBytes(json), id.ToByteArray());

        Assert.Throws<InvalidDataException>(() => _codec.Decode(id, data));
    }

    [Fact]
    public void ARename_RoundTrips()
    {
        var replaced = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var change = new NodeRenamed(Guid.NewGuid(), At, Guid.NewGuid(), "Dune (novel)", replaced);

        var json = Encoding.UTF8.GetString(_keyed.Open(SealKind.Change, _codec.Encode(change), change.Id.ToByteArray()));
        var decoded = Assert.IsType<NodeRenamed>(_codec.Decode(change.Id, _codec.Encode(change)));

        Assert.Contains("\"op\":\"renameNode\"", json);
        Assert.Equal((change.Id, change.At, change.Node, change.Name), (decoded.Id, decoded.At, decoded.Node, decoded.Name));
        Assert.Equal(replaced, decoded.Replaces);
    }

    [Theory]
    [InlineData("{\"format\":1,\"op\":\"renameNode\",\"at\":\"2026-10-16T09:12:00Z\"}")]
    [InlineData("{\"format\":1,\"op\":\"renameNode\",\"at\":\"2026-10-16T09:12:00Z\",\"rename\":{\"name\":\"Dune\",\"replaces\":[\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\"]}}")]
    [InlineData("{\"format\":1,\"op\":\"renameNode\",\"at\":\"2026-10-16T09:12:00Z\",\"rename\":{\"node\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"replaces\":[\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\"]}}")]
    [InlineData("{\"format\":1,\"op\":\"renameNode\",\"at\":\"2026-10-16T09:12:00Z\",\"rename\":{\"node\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"name\":\" \",\"replaces\":[\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\"]}}")]
    [InlineData("{\"format\":1,\"op\":\"renameNode\",\"at\":\"2026-10-16T09:12:00Z\",\"rename\":{\"node\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"name\":\"Dune\",\"replaces\":[]}}")]
    public void AnIncompleteRename_IsRefused(string json)
    {
        var id = Guid.NewGuid();
        var data = _box.Seal("KFC1"u8, Encoding.UTF8.GetBytes(json), id.ToByteArray());

        Assert.Throws<InvalidDataException>(() => _codec.Decode(id, data));
    }

    [Fact]
    public void AChangeOfANewerFormat_AsksForANewerKofrenot()
    {
        var id = Guid.NewGuid();
        var data = _box.Seal("KFC1"u8, Encoding.UTF8.GetBytes("{\"format\":99,\"op\":\"rename\",\"at\":\"2026-10-16T09:12:00Z\"}"), id.ToByteArray());

        Assert.Equal(99, Assert.Throws<NewerVaultFormatException>(() => _codec.Decode(id, data)).Format);
    }

    [Fact]
    public void AChangeOfFormat1_IsStillRead()
    {
        var id = Guid.NewGuid();
        var json = "{\"format\":1,\"op\":\"addNode\",\"at\":\"2026-10-16T09:12:00Z\",\"node\":{\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"type\":\"generic\",\"name\":\"Dune\",\"created\":\"2026-10-16T09:12:00Z\"}}";
        var data = _box.Seal("KFC1"u8, Encoding.UTF8.GetBytes(json), id.ToByteArray());

        Assert.Equal("Dune", Assert.IsType<NodeAdded>(_codec.Decode(id, data)).Node.Name);
        Assert.Equal(1, _codec.DecodeWithFormat(id, data).Format);
    }

    [Fact]
    public void AChange_IsWrittenInTheCurrentFormat()
    {
        var change = new NodeAdded(Guid.NewGuid(), At, new Node(Guid.NewGuid(), "generic", "Dune", At));

        var json = Encoding.UTF8.GetString(_keyed.Open(SealKind.Change, _codec.Encode(change), change.Id.ToByteArray()));

        Assert.StartsWith($"{{\"format\":{VaultFormat.Current},", json);
    }

    [Fact]
    public void AnUnknownChange_CannotBeEncoded() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _codec.Encode(new OtherChange(Guid.NewGuid(), At)));

    private sealed record OtherChange(Guid Id, DateTimeOffset At) : Change(Id, At);
}
