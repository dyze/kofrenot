using System.Security.Cryptography;
using System.Text;

namespace Kofrenot.Core.Tests.Storage;

public sealed class ChangeCodecTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 16, 9, 12, 0, TimeSpan.Zero);

    private readonly SecretBox _box = new(RandomNumberGenerator.GetBytes(SecretBox.KeySize));
    private readonly ChangeCodec _codec;

    public ChangeCodecTests() => _codec = new ChangeCodec(_box);

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

        var json = Encoding.UTF8.GetString(_box.Open("KFC1"u8, _codec.Encode(change), id.ToByteArray()));

        Assert.StartsWith("{\"format\":1,\"op\":\"addNode\",\"at\":", json);
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
    public void AChangeOfANewerFormat_AsksForANewerKofrenot()
    {
        var id = Guid.NewGuid();
        var data = _box.Seal("KFC1"u8, Encoding.UTF8.GetBytes("{\"format\":2,\"op\":\"rename\",\"at\":\"2026-10-16T09:12:00Z\"}"), id.ToByteArray());

        Assert.Equal(2, Assert.Throws<NewerVaultFormatException>(() => _codec.Decode(id, data)).Format);
    }

    [Fact]
    public void AChange_IsWrittenInTheCurrentFormat()
    {
        var change = new NodeAdded(Guid.NewGuid(), At, new Node(Guid.NewGuid(), "generic", "Dune", At));

        var json = Encoding.UTF8.GetString(_box.Open("KFC1"u8, _codec.Encode(change), change.Id.ToByteArray()));

        Assert.StartsWith($"{{\"format\":{VaultFormat.Current},", json);
    }

    [Fact]
    public void AnUnknownChange_CannotBeEncoded() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _codec.Encode(new OtherChange(Guid.NewGuid(), At)));

    private sealed record OtherChange(Guid Id, DateTimeOffset At) : Change(Id, At);
}
