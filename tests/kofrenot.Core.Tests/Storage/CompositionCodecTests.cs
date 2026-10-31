using System.Security.Cryptography;
using System.Text;

namespace Kofrenot.Core.Tests.Storage;

/// <summary>The changes of format 8: node types of the user's, a node's types as a set, field values.</summary>
public sealed class CompositionCodecTests
{
    private const string Node = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";
    private const string Header = "{\"format\":8,\"at\":\"2026-10-16T09:12:00Z\",";

    private static readonly DateTimeOffset At = new(2026, 10, 16, 9, 12, 0, TimeSpan.Zero);
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(SecretBox.KeySize);

    private readonly SecretBox _box = new(Key);
    private readonly ChangeCodec _codec = new(new KeyedBox(KeyRing.Of(Key)));

    private T RoundTrip<T>(T change)
        where T : Change =>
        Assert.IsType<T>(_codec.Decode(change.Id, _codec.Encode(change)));

    private Change Decode(string json)
    {
        var id = Guid.NewGuid();
        return _codec.Decode(id, _box.Seal("KFC1"u8, Encoding.UTF8.GetBytes(json), id.ToByteArray()));
    }

    [Fact]
    public void ANodeType_RoundTrips_WithItsFields()
    {
        FieldDefinition[] fields =
        [
            new("born", "Born", FieldKind.Date),
            new("format", "Format", FieldKind.Choice, ["Novel", "Short story"]),
        ];
        var change = new NodeTypeDefined(Guid.NewGuid(), At, "person", "Person", "Contact", "blue", fields, [Guid.NewGuid()]);

        var decoded = RoundTrip(change);

        Assert.Equal((change.Id, change.At, change.NodeType, change.Name, change.Icon, change.Color), (decoded.Id, decoded.At, decoded.NodeType, decoded.Name, decoded.Icon, decoded.Color));
        Assert.Equal(change.Fields, decoded.Fields);
        Assert.Equal(change.Replaces, decoded.Replaces);
    }

    [Fact]
    public void ANodeType_WithoutIconColourOrFields_RoundTrips()
    {
        var decoded = RoundTrip(new NodeTypeDefined(Guid.NewGuid(), At, "person", "Person", null, null, [], []));

        Assert.Equal((null, null), (decoded.Icon, decoded.Color));
        Assert.Empty(decoded.Fields);
    }

    [Fact]
    public void ANodeType_IsWrittenInTheCurrentFormat_WithLowerCaseKinds()
    {
        var change = new NodeTypeDefined(Guid.NewGuid(), At, "person", "Person", null, null, [new FieldDefinition("born", "Born", FieldKind.Date)], []);

        var json = Encoding.UTF8.GetString(new KeyedBox(KeyRing.Of(Key)).Open(SealKind.Change, _codec.Encode(change), change.Id.ToByteArray()));

        Assert.Contains($"\"format\":{VaultFormat.Current}", json);
        Assert.Contains("\"op\":\"defineNodeType\"", json);
        Assert.Contains("{\"id\":\"born\",\"name\":\"Born\",\"kind\":\"date\"}", json);
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"nodeType\":{\"name\":\"Person\",\"fields\":[],\"replaces\":[]}")]
    [InlineData(",\"nodeType\":{\"id\":\"p\",\"name\":\" \",\"fields\":[],\"replaces\":[]}")]
    [InlineData(",\"nodeType\":{\"id\":\"p\",\"name\":\"Person\",\"replaces\":[]}")]
    [InlineData(",\"nodeType\":{\"id\":\"p\",\"name\":\"Person\",\"fields\":[]}")]
    [InlineData(",\"nodeType\":{\"id\":\"p\",\"name\":\"Person\",\"icon\":\"\",\"fields\":[],\"replaces\":[]}")]
    [InlineData(",\"nodeType\":{\"id\":\"p\",\"name\":\"Person\",\"color\":\"\",\"fields\":[],\"replaces\":[]}")]
    [InlineData(",\"nodeType\":{\"id\":\"p\",\"name\":\"Person\",\"fields\":[null],\"replaces\":[]}")]
    [InlineData(",\"nodeType\":{\"id\":\"p\",\"name\":\"Person\",\"fields\":[{\"name\":\"Born\",\"kind\":\"date\"}],\"replaces\":[]}")]
    [InlineData(",\"nodeType\":{\"id\":\"p\",\"name\":\"Person\",\"fields\":[{\"id\":\"b\",\"name\":\"\",\"kind\":\"date\"}],\"replaces\":[]}")]
    [InlineData(",\"nodeType\":{\"id\":\"p\",\"name\":\"Person\",\"fields\":[{\"id\":\"b\",\"name\":\"Born\",\"kind\":\"markdown\"}],\"replaces\":[]}")]
    [InlineData(",\"nodeType\":{\"id\":\"p\",\"name\":\"Person\",\"fields\":[{\"id\":\"b\",\"name\":\"Born\",\"kind\":\"date\",\"choices\":[]}],\"replaces\":[]}")]
    [InlineData(",\"nodeType\":{\"id\":\"p\",\"name\":\"Person\",\"fields\":[{\"id\":\"f\",\"name\":\"Format\",\"kind\":\"choice\"}],\"replaces\":[]}")]
    [InlineData(",\"nodeType\":{\"id\":\"p\",\"name\":\"Person\",\"fields\":[{\"id\":\"f\",\"name\":\"Format\",\"kind\":\"choice\",\"choices\":[\" \"]}],\"replaces\":[]}")]
    [InlineData(",\"nodeType\":{\"id\":\"p\",\"name\":\"Person\",\"fields\":[{\"id\":\"b\",\"name\":\"Born\",\"kind\":\"date\"},{\"id\":\"b\",\"name\":\"Died\",\"kind\":\"date\"}],\"replaces\":[]}")]
    public void AnIncompleteNodeType_IsRefused(string type)
    {
        Assert.Throws<InvalidDataException>(() => Decode(Header + "\"op\":\"defineNodeType\"" + type + "}"));
    }

    [Fact]
    public void ATypeAdded_RoundTrips()
    {
        var change = new NodeTypeAdded(Guid.NewGuid(), At, Guid.NewGuid(), "person");

        Assert.Equal(change, RoundTrip(change));
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"addType\":{\"type\":\"person\"}")]
    [InlineData(",\"addType\":{\"node\":\"" + Node + "\"}")]
    [InlineData(",\"addType\":{\"node\":\"" + Node + "\",\"type\":\"\"}")]
    public void AnIncompleteTypeAdded_IsRefused(string type)
    {
        Assert.Throws<InvalidDataException>(() => Decode(Header + "\"op\":\"addNodeType\"" + type + "}"));
    }

    [Fact]
    public void ATypeRemoved_RoundTrips()
    {
        var change = new NodeTypeRemoved(Guid.NewGuid(), At, Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid()]);

        var decoded = RoundTrip(change);

        Assert.Equal((change.Id, change.At, change.Node), (decoded.Id, decoded.At, decoded.Node));
        Assert.Equal(change.Removes, decoded.Removes);
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"removeType\":{\"removes\":[\"" + Node + "\"]}")]
    [InlineData(",\"removeType\":{\"node\":\"" + Node + "\"}")]
    [InlineData(",\"removeType\":{\"node\":\"" + Node + "\",\"removes\":[]}")]
    public void AnIncompleteTypeRemoved_IsRefused(string type)
    {
        Assert.Throws<InvalidDataException>(() => Decode(Header + "\"op\":\"removeNodeType\"" + type + "}"));
    }

    [Theory]
    [InlineData(FieldKind.Date, "1920-10-08")]
    [InlineData(FieldKind.Checkbox, "true")]
    [InlineData(FieldKind.Email, "ann@example.com")]
    [InlineData(FieldKind.Address, "12 rue de la Paix, Vendôme")]
    [InlineData(FieldKind.Text, null)]
    public void AFieldValue_RoundTrips(FieldKind kind, string? value)
    {
        var change = new FieldSet(Guid.NewGuid(), At, Guid.NewGuid(), "person", "born", "Born", kind, value, [Guid.NewGuid()]);

        var decoded = RoundTrip(change);

        Assert.Equal(
            (change.Id, change.At, change.Node, change.Owner, change.Field, change.Name, change.Kind, change.Value),
            (decoded.Id, decoded.At, decoded.Node, decoded.Owner, decoded.Field, decoded.Name, decoded.Kind, decoded.Value));
        Assert.Equal(change.Replaces, decoded.Replaces);
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"field\":{\"owner\":\"own\",\"id\":\"b\",\"name\":\"Born\",\"kind\":\"date\",\"replaces\":[]}")]
    [InlineData(",\"field\":{\"node\":\"" + Node + "\",\"id\":\"b\",\"name\":\"Born\",\"kind\":\"date\",\"replaces\":[]}")]
    [InlineData(",\"field\":{\"node\":\"" + Node + "\",\"owner\":\"own\",\"name\":\"Born\",\"kind\":\"date\",\"replaces\":[]}")]
    [InlineData(",\"field\":{\"node\":\"" + Node + "\",\"owner\":\"own\",\"id\":\"b\",\"name\":\" \",\"kind\":\"date\",\"replaces\":[]}")]
    [InlineData(",\"field\":{\"node\":\"" + Node + "\",\"owner\":\"own\",\"id\":\"b\",\"name\":\"Born\",\"kind\":\"file\",\"replaces\":[]}")]
    [InlineData(",\"field\":{\"node\":\"" + Node + "\",\"owner\":\"own\",\"id\":\"b\",\"name\":\"Born\",\"kind\":\"date\"}")]
    [InlineData(",\"field\":{\"node\":\"" + Node + "\",\"owner\":\"own\",\"id\":\"b\",\"name\":\"Born\",\"kind\":\"date\",\"value\":\"soon\",\"replaces\":[]}")]
    public void AnIncompleteFieldValue_IsRefused(string field)
    {
        Assert.Throws<InvalidDataException>(() => Decode(Header + "\"op\":\"setField\"" + field + "}"));
    }

    [Fact]
    public void AChangeOfANewerFormat_AsksForANewerKofrenot()
    {
        Assert.Throws<NewerVaultFormatException>(() => Decode("{\"format\":10,\"at\":\"2026-10-16T09:12:00Z\",\"op\":\"setField\"}"));
    }
}
