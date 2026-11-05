using System.Text;

namespace Kofrenot.Core.Tests.Storage;

/// <summary>The skip rules: a change of a newer format that says this kofrenot may read it (<c>oldestReader</c>).</summary>
public sealed partial class ChangeCodecTests
{
    private const int Newer = VaultFormat.Current + 1;

    private const string NewerAt = "\"at\":\"2026-10-16T09:12:00Z\"";

    [Fact]
    public void ANewerChange_ThisKofrenotMayRead_IsRead_ItsUnknownPropertiesIgnored()
    {
        var decoded = DecodeJson($"{{\"format\":{Newer},\"oldestReader\":{VaultFormat.Current},\"oldestWriter\":{Newer},\"op\":\"addNode\",{NewerAt},\"event\":{{}},"
            + "\"node\":{\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"type\":\"generic\",\"name\":\"Dune\",\"created\":\"2026-10-16T09:12:00Z\",\"role\":\"work\"}}");

        Assert.Equal("Dune", Assert.IsType<NodeAdded>(decoded.Change).Node.Name);
        Assert.Equal((Newer, Newer), (decoded.Format, decoded.OldestWriter));
    }

    [Fact]
    public void AChange_WithoutWrites_WritesFromItsFormat() =>
        Assert.Equal(1, DecodeJson("{\"format\":1,\"op\":\"addNode\"," + NewerAt
            + ",\"node\":{\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"type\":\"generic\",\"name\":\"Dune\",\"created\":\"2026-10-16T09:12:00Z\"}}").OldestWriter);

    [Theory]
    [InlineData("\"op\":\"defineEvent\"")]
    [InlineData("\"op\":\"setField\",\"field\":{\"link\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"owner\":\"own\",\"id\":\"date\",\"name\":\"Date\",\"kind\":\"date\",\"value\":\"1965-01-01\",\"replaces\":[]}")]
    public void ANewerChange_ThisKofrenotDoesNotKnow_IsSkipped(string op)
    {
        var json = $"{{\"format\":{Newer},\"oldestReader\":{VaultFormat.Current},{op},{NewerAt}}}";

        Assert.Null(DecodeJson(json).Change);
        var id = Guid.NewGuid();
        Assert.Throws<InvalidDataException>(() => _codec.Decode(id, Seal(id, json)));
    }

    [Fact]
    public void ANewerChange_ThisKofrenotMayNotRead_AsksForANewerKofrenot() =>
        Assert.Equal(Newer, Assert.Throws<NewerVaultFormatException>(() => DecodeJson($"{{\"format\":{Newer},\"oldestReader\":{Newer},\"op\":\"defineEvent\",{NewerAt}}}")).Format);

    [Theory]
    [InlineData("\"oldestReader\":0")]
    [InlineData("\"oldestReader\":99")]
    [InlineData("\"oldestWriter\":0")]
    [InlineData("\"oldestWriter\":99")]
    public void AChange_WithLevelsOutsideItsFormat_IsDamaged(string level) =>
        Assert.Throws<InvalidDataException>(() => DecodeJson($"{{\"format\":{Newer},{level},\"op\":\"defineEvent\",{NewerAt}}}"));

    [Fact]
    public void ANewerChange_WithAFieldOfAnUnknownKind_KeepsIt()
    {
        var defined = Assert.IsType<NodeTypeDefined>(DecodeJson($"{{\"format\":{Newer},\"oldestReader\":{VaultFormat.Current},\"op\":\"defineNodeType\",{NewerAt},"
            + "\"nodeType\":{\"id\":\"book\",\"name\":\"Book\",\"fields\":[{\"id\":\"stars\",\"name\":\"Stars\",\"kind\":\"rating\",\"choices\":[\"1\"]}],\"replaces\":[]}}").Change);
        var set = Assert.IsType<FieldSet>(DecodeJson($"{{\"format\":{Newer},\"oldestReader\":{VaultFormat.Current},\"op\":\"setField\",{NewerAt},"
            + "\"field\":{\"node\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"owner\":\"own\",\"id\":\"stars\",\"name\":\"Stars\",\"kind\":\"rating\",\"value\":\"4/5\",\"replaces\":[]}}").Change);

        Assert.Equal(new FieldDefinition("stars", "Stars", FieldKind.Unknown), Assert.Single(defined.Fields));
        Assert.Equal((FieldKind.Unknown, "4/5", null), (set.Kind, set.Value, set.File));
    }

    [Theory]
    [InlineData("rating")]
    [InlineData("markdown")]
    public void AChangeOfThisFormat_WithAFieldOfAKindItDoesNotWrite_IsDamaged(string kind) =>
        Assert.Throws<InvalidDataException>(() => DecodeJson($"{{\"format\":{VaultFormat.Current},\"op\":\"setField\",{NewerAt},"
            + $"\"field\":{{\"node\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"owner\":\"own\",\"id\":\"stars\",\"name\":\"Stars\",\"kind\":\"{kind}\",\"value\":\"4\",\"replaces\":[]}}}}"));

    [Fact]
    public void ANewerChange_WithAFieldOfAContentKind_IsSkipped() =>
        Assert.Null(DecodeJson($"{{\"format\":{Newer},\"oldestReader\":{VaultFormat.Current},\"op\":\"setField\",{NewerAt},"
            + "\"field\":{\"node\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"owner\":\"own\",\"id\":\"text\",\"name\":\"Text\",\"kind\":\"markdown\",\"value\":\"4\",\"replaces\":[]}}").Change);

    private DecodedChange DecodeJson(string json)
    {
        var id = Guid.NewGuid();
        return _codec.DecodeWithFormat(id, Seal(id, json));
    }

    private byte[] Seal(Guid id, string json) => _box.Seal("KFC1"u8, Encoding.UTF8.GetBytes(json), id.ToByteArray());
}
