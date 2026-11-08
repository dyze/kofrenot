using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Kofrenot.Core.Tests.Storage;

/// <summary>The change of format 12: a mapping's definition, written so that a kofrenot of format 10 (0.15) still reads the vault, skipping it.</summary>
public sealed class MappingCodecTests
{
    private const string Node = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";
    private const string Header = "{\"format\":12,\"oldestReader\":10,\"oldestWriter\":11,\"at\":\"2026-10-16T09:12:00Z\",\"op\":\"editMapping\",\"mapping\":";

    private static readonly DateTimeOffset At = new(2026, 10, 16, 9, 12, 0, TimeSpan.Zero);
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(SecretBox.KeySize);

    private static readonly MappingDefinition Books = new(
        MappingDefinition.Json,
        "book",
        ["title_fr", "title"],
        "isbn",
        ["original-title"],
        [new FieldRule("title", "original-title"), new FieldRule("publisher.name", "publisher")],
        [new LinkRule("authors", "person", "wrote", IsReverse: true), new LinkRule("series", "series", "contains", IsReverse: false)]);

    private readonly SecretBox _box = new(Key);
    private readonly KeyedBox _keyed = new(KeyRing.Of(Key));
    private readonly ChangeCodec _codec;

    public MappingCodecTests() => _codec = new ChangeCodec(_keyed);

    [Fact]
    public void AMappingVersion_RoundTrips_WithItsDefinition()
    {
        var change = new MappingEdited(Guid.NewGuid(), At, Guid.NewGuid(), Books, [Guid.NewGuid()]);

        var decoded = Assert.IsType<MappingEdited>(_codec.Decode(change.Id, _codec.Encode(change)));

        Assert.Equal((change.Id, change.At, change.Node, change.Definition), (decoded.Id, decoded.At, decoded.Node, decoded.Definition));
        Assert.Equal(change.Replaces, decoded.Replaces);
    }

    [Fact]
    public void AMappingWithoutMatch_RoundTrips()
    {
        var change = new MappingEdited(Guid.NewGuid(), At, Guid.NewGuid(), Books with { Match = null, OtherNames = [], Fields = [], Links = [] }, []);

        Assert.Equal(change.Definition, Assert.IsType<MappingEdited>(_codec.Decode(change.Id, _codec.Encode(change))).Definition);
        Assert.False(Json(change)["mapping"]!.AsObject().ContainsKey("match"));
    }

    [Fact]
    public void AMappingVersion_HasTheDocumentedShape_ReadableByOlderFormats()
    {
        var node = Guid.NewGuid();
        var change = new MappingEdited(Guid.NewGuid(), At, node, Books with { Fields = [new FieldRule("isbn", "isbn")], Links = [Books.Links[0]] }, []);

        var json = Json(change);

        Assert.Equal((12, 10, 11, "editMapping"), ((int)json["format"]!, (int)json["oldestReader"]!, (int)json["oldestWriter"]!, (string)json["op"]!));
        Assert.Equal(
            $"{{\"node\":\"{node}\",\"source\":\"json\",\"nodeType\":\"book\",\"name\":[\"title_fr\",\"title\"],\"match\":\"isbn\",\"otherNames\":[\"original-title\"],"
            + "\"fields\":[{\"key\":\"isbn\",\"field\":\"isbn\"}],\"links\":[{\"key\":\"authors\",\"nodeType\":\"person\",\"linkType\":\"wrote\",\"reverse\":true}],\"replaces\":[]}",
            json["mapping"]!.ToJsonString());
    }

    [Theory]
    [InlineData("{\"source\":\"json\",\"nodeType\":\"book\",\"name\":[\"title\"],\"replaces\":[]}")]
    [InlineData("{\"node\":\"" + Node + "\",\"source\":\"csv\",\"nodeType\":\"book\",\"name\":[\"title\"],\"replaces\":[]}")]
    [InlineData("{\"node\":\"" + Node + "\",\"source\":\"json\",\"name\":[\"title\"],\"replaces\":[]}")]
    [InlineData("{\"node\":\"" + Node + "\",\"source\":\"json\",\"nodeType\":\"book\",\"replaces\":[]}")]
    [InlineData("{\"node\":\"" + Node + "\",\"source\":\"json\",\"nodeType\":\"book\",\"name\":[],\"replaces\":[]}")]
    [InlineData("{\"node\":\"" + Node + "\",\"source\":\"json\",\"nodeType\":\"book\",\"name\":[\"\"],\"replaces\":[]}")]
    [InlineData("{\"node\":\"" + Node + "\",\"source\":\"json\",\"nodeType\":\"book\",\"name\":[\"title\"]}")]
    [InlineData("{\"node\":\"" + Node + "\",\"source\":\"json\",\"nodeType\":\"book\",\"name\":[\"title\"],\"match\":\"\",\"replaces\":[]}")]
    [InlineData("{\"node\":\"" + Node + "\",\"source\":\"json\",\"nodeType\":\"book\",\"name\":[\"title\"],\"otherNames\":[\"\"],\"replaces\":[]}")]
    [InlineData("{\"node\":\"" + Node + "\",\"source\":\"json\",\"nodeType\":\"book\",\"name\":[\"title\"],\"fields\":[{\"key\":\"isbn\"}],\"replaces\":[]}")]
    [InlineData("{\"node\":\"" + Node + "\",\"source\":\"json\",\"nodeType\":\"book\",\"name\":[\"title\"],\"fields\":[null],\"replaces\":[]}")]
    [InlineData("{\"node\":\"" + Node + "\",\"source\":\"json\",\"nodeType\":\"book\",\"name\":[\"title\"],\"links\":[{\"key\":\"authors\",\"nodeType\":\"person\"}],\"replaces\":[]}")]
    public void AnIncompleteMappingVersion_IsRefused(string mapping) =>
        Assert.Throws<InvalidDataException>(() => Decode(Header + mapping + "}"));

    [Fact]
    public void AMapping_ReadWithoutItsOptionalLists_HasThemEmpty()
    {
        var decoded = Assert.IsType<MappingEdited>(Decode(Header + "{\"node\":\"" + Node + "\",\"source\":\"json\",\"nodeType\":\"book\",\"name\":[\"title\"],\"replaces\":[]}}"));

        Assert.Equal(new MappingDefinition(MappingDefinition.Json, "book", ["title"], null, [], [], []), decoded.Definition);
    }

    [Fact]
    public void ADefinition_EqualsAnotherByValue()
    {
        var copy = Books with { NameKeys = [.. Books.NameKeys], Fields = [.. Books.Fields] };

        Assert.Equal(Books, copy);
        Assert.Equal(Books.GetHashCode(), copy.GetHashCode());
        Assert.NotEqual(Books, Books with { OtherNames = [] });
        Assert.False(Books.Equals(null));
    }

    private JsonNode Json(Change change) => JsonNode.Parse(_keyed.Open(SealKind.Change, _codec.Encode(change), change.Id.ToByteArray()))!;

    private Change Decode(string json)
    {
        var id = Guid.NewGuid();
        return _codec.Decode(id, _box.Seal("KFC1"u8, Encoding.UTF8.GetBytes(json), id.ToByteArray()));
    }
}
