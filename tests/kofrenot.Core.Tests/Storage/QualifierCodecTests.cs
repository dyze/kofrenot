using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Kofrenot.Core.Tests.Storage;

/// <summary>
/// The changes of format 11: qualifiers (fields on links) and the fields of link types, written so that
/// a kofrenot of format 10 (0.15) still reads the vault, skipping them.
/// </summary>
public sealed class QualifierCodecTests
{
    private const string Link = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";
    private const string Header = "{\"format\":11,\"oldestReader\":10,\"oldestWriter\":11,\"at\":\"2026-10-16T09:12:00Z\",";

    private static readonly DateTimeOffset At = new(2026, 10, 16, 9, 12, 0, TimeSpan.Zero);
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(SecretBox.KeySize);

    private readonly SecretBox _box = new(Key);
    private readonly KeyedBox _keyed = new(KeyRing.Of(Key));
    private readonly ChangeCodec _codec;

    public QualifierCodecTests() => _codec = new ChangeCodec(_keyed);

    [Fact]
    public void AQualifier_RoundTrips_OnItsLink()
    {
        var change = new FieldSet(Guid.NewGuid(), At, FieldHolder.OfLink(Guid.NewGuid()), "wrote", "date", "Date", FieldKind.Date, "1965-08-01", [Guid.NewGuid()]);

        var decoded = Assert.IsType<FieldSet>(_codec.Decode(change.Id, _codec.Encode(change)));

        Assert.Equal((change.Holder, change.Owner, change.Field, change.Value), (decoded.Holder, decoded.Owner, decoded.Field, decoded.Value));
        Assert.True(decoded.Holder.IsLink);
    }

    [Theory]
    [InlineData("1965")]
    [InlineData("1965-08")]
    public void AVagueDate_RoundTrips(string value)
    {
        var change = new FieldSet(Guid.NewGuid(), At, FieldHolder.OfLink(Guid.NewGuid()), "wrote", "date", "Date", FieldKind.Date, value, []);

        Assert.Equal(value, Assert.IsType<FieldSet>(_codec.Decode(change.Id, _codec.Encode(change))).Value);
    }

    [Fact]
    public void AQualifier_IsWrittenOnItsLink_WithoutNode_SoAnOlderKofrenotSkipsIt()
    {
        var change = new FieldSet(Guid.NewGuid(), At, FieldHolder.OfLink(Guid.NewGuid()), FieldSet.Own, "source", "Source", FieldKind.Url, "https://isfdb.org", []);

        var field = Json(change)["field"]!.AsObject();

        Assert.Equal(change.Holder.Id.ToString(), (string?)field["link"]);
        Assert.False(field.ContainsKey("node"));
    }

    [Fact]
    public void ANodeValue_IsWrittenOnItsNode_WithoutLink()
    {
        var change = new FieldSet(Guid.NewGuid(), At, FieldHolder.OfNode(Guid.NewGuid()), FieldSet.Own, "born", "Born", FieldKind.Text, "Tacoma", []);

        var field = Json(change)["field"]!.AsObject();

        Assert.Equal(change.Holder.Id.ToString(), (string?)field["node"]);
        Assert.False(field.ContainsKey("link"));
    }

    [Theory]
    [InlineData("\"node\":\"" + Link + "\",\"link\":\"" + Link + "\",")]
    [InlineData("\"link\":\"00000000-0000-0000-0000-000000000000\",")]
    public void AQualifier_OnBothANodeAndALink_OrOnNone_IsRefused(string holder) =>
        Assert.Throws<InvalidDataException>(() => Decode(Header + "\"op\":\"setField\",\"field\":{" + holder
            + "\"owner\":\"own\",\"id\":\"d\",\"name\":\"Date\",\"kind\":\"date\",\"value\":\"1965-08-01\",\"replaces\":[]}}"));

    [Fact]
    public void ALinkType_RoundTrips_WithItsFields()
    {
        FieldDefinition[] fields = [new("date", "Date", FieldKind.Date), new("format", "Format", FieldKind.Choice, ["Novel", "Short story"]), new("cover", "Cover", FieldKind.Image)];
        var change = new LinkTypeDefined(Guid.NewGuid(), At, "wrote", "wrote", "written by", fields, []);

        var decoded = Assert.IsType<LinkTypeDefined>(_codec.Decode(change.Id, _codec.Encode(change)));

        Assert.Equal(fields, decoded.Fields);
        Assert.Equal("wrote", (string?)Json(change)["linkType"]!["name"]);
    }

    [Fact]
    public void ALinkType_OfAnOlderFormat_HasNoFields() =>
        Assert.Empty(Assert.IsType<LinkTypeDefined>(Decode("{\"format\":10,\"at\":\"2026-10-16T09:12:00Z\",\"op\":\"defineLinkType\","
            + "\"linkType\":{\"id\":\"wrote\",\"name\":\"wrote\",\"replaces\":[]}}")).Fields);

    [Theory]
    [InlineData("[{\"id\":\"d\",\"name\":\"Date\",\"kind\":\"date\"},{\"id\":\"d\",\"name\":\"Day\",\"kind\":\"date\"}]")]
    [InlineData("[{\"id\":\"d\",\"name\":\"Date\"}]")]
    [InlineData("[{\"id\":\"d\",\"name\":\"Text\",\"kind\":\"markdown\"}]")]
    [InlineData("[null]")]
    public void ALinkType_WithAFieldIncomplete_OrTwice_IsRefused(string fields) =>
        Assert.Throws<InvalidDataException>(() => Decode(Header + "\"op\":\"defineLinkType\",\"linkType\":{\"id\":\"wrote\",\"name\":\"wrote\",\"fields\":" + fields + ",\"replaces\":[]}}"));

    [Fact]
    public void ANewerLinkType_WithAFieldOfAnUnknownKind_KeepsIt() =>
        Assert.Equal(
            FieldKind.Unknown,
            Assert.Single(Assert.IsType<LinkTypeDefined>(Decode("{\"format\":12,\"oldestReader\":11,\"at\":\"2026-10-16T09:12:00Z\",\"op\":\"defineLinkType\","
                + "\"linkType\":{\"id\":\"wrote\",\"name\":\"wrote\",\"fields\":[{\"id\":\"s\",\"name\":\"Stars\",\"kind\":\"rating\"}],\"replaces\":[]}}")).Fields).Kind);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ALink_RoundTrips_SeparateOrNot_WrittenSeparateOnlyWhenItIs(bool separate)
    {
        var change = new LinkAdded(Guid.NewGuid(), At, new Link(Guid.NewGuid(), "married-to", Guid.NewGuid(), Guid.NewGuid(), At, separate));

        var decoded = Assert.IsType<LinkAdded>(_codec.Decode(change.Id, _codec.Encode(change)));

        Assert.Equal(change.Link, decoded.Link);
        Assert.Equal(separate, Json(change)["link"]!.AsObject().ContainsKey("separate"));
    }

    private JsonNode Json(Change change) => JsonNode.Parse(_keyed.Open(SealKind.Change, _codec.Encode(change), change.Id.ToByteArray()))!;

    private Change Decode(string json)
    {
        var id = Guid.NewGuid();
        return _codec.Decode(id, _box.Seal("KFC1"u8, Encoding.UTF8.GetBytes(json), id.ToByteArray()));
    }
}
