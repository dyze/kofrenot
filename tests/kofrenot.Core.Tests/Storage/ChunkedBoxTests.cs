using System.Security.Cryptography;

namespace Kofrenot.Core.Tests.Storage;

public sealed class ChunkedBoxTests
{
    private const int Header = 4 + 8;
    private const int Tag = 16;
    private static readonly byte[] Place = [1, 2, 3];

    private readonly byte[] _key = RandomNumberGenerator.GetBytes(SecretBox.KeySize);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(ChunkedBox.ChunkSize - 1)]
    [InlineData(ChunkedBox.ChunkSize)]
    [InlineData(ChunkedBox.ChunkSize + 1)]
    [InlineData((2 * ChunkedBox.ChunkSize) + 5)]
    public void AnySize_RoundTrips(int size)
    {
        var plain = RandomNumberGenerator.GetBytes(size);

        var sealedData = Seal(plain);

        Assert.Equal(plain, Open(sealedData));
        var chunks = Math.Max(1, (size + ChunkedBox.ChunkSize - 1) / ChunkedBox.ChunkSize);
        Assert.Equal(Header + size + (chunks * Tag), sealedData.Length);
    }

    [Fact]
    public void TheSameContent_IsSealedDifferentlyEachTime()
    {
        var plain = new byte[100];

        Assert.NotEqual(Seal(plain), Seal(plain));
    }

    [Fact]
    public void ReadingInSmallPieces_GivesTheWholeContent()
    {
        var plain = RandomNumberGenerator.GetBytes(ChunkedBox.ChunkSize + 10);
        using var opened = Box().Open("KFD1"u8, new MemoryStream(Seal(plain)), Place);
        var read = new MemoryStream();
        var buffer = new byte[7777];
        int count;
        while ((count = opened.Read(buffer, 0, buffer.Length)) > 0)
        {
            read.Write(buffer, 0, count);
        }

        Assert.Equal(plain, read.ToArray());
        Assert.Equal(0, opened.Read(buffer, 0, buffer.Length));
    }

    [Fact]
    public void AnotherKey_OrPlace_DoesNotOpen()
    {
        var sealedData = Seal([1, 2, 3]);

        Assert.Throws<InvalidDataException>(() => Open(sealedData, new ChunkedBox(RandomNumberGenerator.GetBytes(SecretBox.KeySize))));
        Assert.Throws<InvalidDataException>(() => Open(sealedData, place: [9]));
    }

    [Fact]
    public void AnotherKindOfData_OrAHeaderCutOff_IsRefusedAtOnce()
    {
        var sealedData = Seal([1, 2, 3]);

        Assert.Throws<InvalidDataException>(() => Box().Open("KFN1"u8, new MemoryStream(sealedData), Place));
        Assert.Throws<InvalidDataException>(() => Box().Open("KFD1"u8, new MemoryStream(sealedData[..5]), Place));
    }

    [Fact]
    public void AChangedByte_DoesNotOpen()
    {
        var sealedData = Seal(new byte[10]);
        sealedData[Header + 2] ^= 1;

        Assert.Throws<InvalidDataException>(() => Open(sealedData));
    }

    [Fact]
    public void AFileCutAfterAFullChunk_OrInsideAChunk_DoesNotOpen()
    {
        var sealedData = Seal(new byte[ChunkedBox.ChunkSize + 10]);
        var firstChunkEnd = Header + ChunkedBox.ChunkSize + Tag;

        Assert.Throws<InvalidDataException>(() => Open(sealedData[..firstChunkEnd]));
        Assert.Throws<InvalidDataException>(() => Open(sealedData[..(firstChunkEnd + 5)]));
        Assert.Throws<InvalidDataException>(() => Open(sealedData[..(Header + 5)]));
    }

    [Fact]
    public void SwappedChunks_OrDataAddedAfterTheLast_DoNotOpen()
    {
        var plain = new byte[2 * ChunkedBox.ChunkSize];
        var sealedData = Seal(plain);
        var record = ChunkedBox.ChunkSize + Tag;
        var swapped = sealedData[..Header]
            .Concat(sealedData[(Header + record)..])
            .Concat(sealedData[Header..(Header + record)])
            .ToArray();

        Assert.Throws<InvalidDataException>(() => Open(swapped));
        Assert.Throws<InvalidDataException>(() => Open([.. Seal([1, 2, 3]), .. new byte[Tag]]));
    }

    [Fact]
    public void TheOpenedStream_OnlyReads()
    {
        using var opened = Box().Open("KFD1"u8, new MemoryStream(Seal([1])), Place);

        Assert.True(opened.CanRead);
        Assert.False(opened.CanSeek);
        Assert.False(opened.CanWrite);
        Assert.Throws<NotSupportedException>(() => opened.Length);
        Assert.Throws<NotSupportedException>(() => opened.Position);
        Assert.Throws<NotSupportedException>(() => opened.Position = 0);
        Assert.Throws<NotSupportedException>(() => opened.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => opened.SetLength(0));
        Assert.Throws<NotSupportedException>(() => opened.Write([1], 0, 1));
        opened.Flush();
    }

    [Fact]
    public void AWrongKeySize_OrMagicSize_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => new ChunkedBox(new byte[16]));
        Assert.Throws<ArgumentException>(() => Box().Seal("KF"u8, new MemoryStream(), new MemoryStream(), Place));
    }

    private ChunkedBox Box() => new(_key);

    private byte[] Seal(byte[] plain)
    {
        var output = new MemoryStream();
        Box().Seal("KFD1"u8, new MemoryStream(plain), output, Place);
        return output.ToArray();
    }

    private byte[] Open(byte[] sealedData, ChunkedBox? box = null, byte[]? place = null)
    {
        using var opened = (box ?? Box()).Open("KFD1"u8, new MemoryStream(sealedData), place ?? Place);
        var plain = new MemoryStream();
        opened.CopyTo(plain);
        return plain.ToArray();
    }
}
