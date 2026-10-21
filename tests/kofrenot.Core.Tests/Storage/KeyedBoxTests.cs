using System.Security.Cryptography;

namespace Kofrenot.Core.Tests.Storage;

public sealed class KeyedBoxTests
{
    private static readonly byte[] Place = [1, 2, 3];
    private static readonly byte[] Plain = [10, 20, 30];
    private static readonly byte[] Old = RandomNumberGenerator.GetBytes(SecretBox.KeySize);
    private static readonly byte[] New = RandomNumberGenerator.GetBytes(SecretBox.KeySize);

    private readonly KeyedBox _box = new(new KeyRing(new Dictionary<int, byte[]> { [0] = Old, [2] = New }));

    [Fact]
    public void ANewFile_StartsWithItsMagicAndEpoch_AndOpens()
    {
        var sealedData = _box.Seal(SealKind.Change, Plain, Place);

        Assert.Equal([.. "KFC2"u8, 0, 0, 0, 2], sealedData[..SealKind.HeaderSize]);
        Assert.Equal(Plain, _box.Open(SealKind.Change, sealedData, Place));
        Assert.Throws<InvalidDataException>(() => new KeyedBox(KeyRing.Of(New)).Open(SealKind.Change, sealedData, Place));
    }

    [Fact]
    public void AFileWrittenBeforeFormat6_OpensWithEpoch0()
    {
        var legacy = new SecretBox(Old).Seal("KFN1"u8, Plain, Place);

        Assert.Equal(Plain, _box.Open(SealKind.Note, legacy, Place));
    }

    [Fact]
    public void TheEpoch_CannotBeChanged()
    {
        var sealedData = _box.Seal(SealKind.Thumbnail, Plain, Place);
        sealedData[SealKind.HeaderSize - 1] = 0;

        Assert.Throws<InvalidDataException>(() => _box.Open(SealKind.Thumbnail, sealedData, Place));
    }

    [Fact]
    public void AnotherKind_AnUnknownEpoch_OrAHeaderCutOff_IsRefused()
    {
        var sealedData = _box.Seal(SealKind.Change, Plain, Place);
        byte[] unknownEpoch = [.. "KFC2"u8, 0, 0, 0, 1, .. sealedData[SealKind.HeaderSize..]];
        byte[] hugeEpoch = [.. "KFC2"u8, 0xFF, 0xFF, 0xFF, 0xFF, .. sealedData[SealKind.HeaderSize..]];

        Assert.Throws<InvalidDataException>(() => _box.Open(SealKind.Note, sealedData, Place));
        Assert.Throws<InvalidDataException>(() => _box.Open(SealKind.Change, unknownEpoch, Place));
        Assert.Throws<InvalidDataException>(() => _box.Open(SealKind.Change, hugeEpoch, Place));
        Assert.Throws<InvalidDataException>(() => _box.Open(SealKind.Change, sealedData[..6], Place));
    }

    [Fact]
    public void AStream_IsSealedWithTheCurrentEpoch_AndOpens()
    {
        var output = new MemoryStream();
        _box.Seal(SealKind.Dataset, new MemoryStream(Plain), output, Place);
        var sealedData = output.ToArray();

        Assert.Equal([.. "KFD2"u8, 0, 0, 0, 2], sealedData[..SealKind.HeaderSize]);
        Assert.Equal(Plain, Read(_box.Open(SealKind.Dataset, new MemoryStream(sealedData), Place)));
    }

    [Fact]
    public void AStreamWrittenBeforeFormat6_OpensWithEpoch0()
    {
        var output = new MemoryStream();
        new ChunkedBox(Old).Seal("KFD1"u8, new MemoryStream(Plain), output, Place);

        Assert.Equal(Plain, Read(_box.Open(SealKind.Dataset, new MemoryStream(output.ToArray()), Place)));
    }

    [Fact]
    public void AStreamOfAnotherKind_IsRefused_AndDisposed()
    {
        var sealedData = new MemoryStream(_box.Seal(SealKind.Change, Plain, Place));

        Assert.Throws<InvalidDataException>(() => _box.Open(SealKind.Dataset, sealedData, Place));
        Assert.False(sealedData.CanRead);
    }

    private static byte[] Read(Stream opened)
    {
        using (opened)
        {
            var plain = new MemoryStream();
            opened.CopyTo(plain);
            return plain.ToArray();
        }
    }
}
