using System.Security.Cryptography;
using System.Text;

namespace Kofrenot.Core.Tests.Storage;

public sealed class SecretBoxTests
{
    private static readonly byte[] Plain = Encoding.UTF8.GetBytes("Dune relates to Frank Herbert");
    private static readonly byte[] Place = [1, 2, 3];

    private readonly SecretBox _box = new(RandomNumberGenerator.GetBytes(SecretBox.KeySize));

    [Fact]
    public void SealedData_Opens()
    {
        var sealedData = _box.Seal("TEST"u8, Plain, Place);

        Assert.Equal(Plain, _box.Open("TEST"u8, sealedData, Place));
    }

    [Fact]
    public void SealedData_StartsWithItsMagic_AndHidesThePlainText()
    {
        var sealedData = _box.Seal("TEST"u8, Plain, Place);

        Assert.Equal("TEST"u8.ToArray(), sealedData[..4]);
        Assert.DoesNotContain("Dune", Encoding.UTF8.GetString(sealedData));
    }

    [Fact]
    public void SealingTwice_GivesDifferentData() =>
        Assert.NotEqual(_box.Seal("TEST"u8, Plain, Place), _box.Seal("TEST"u8, Plain, Place));

    [Fact]
    public void EmptyData_SealsAndOpens() =>
        Assert.Empty(_box.Open("TEST"u8, _box.Seal("TEST"u8, [], Place), Place));

    [Fact]
    public void AnotherKey_CannotOpen()
    {
        var sealedData = _box.Seal("TEST"u8, Plain, Place);
        var other = new SecretBox(RandomNumberGenerator.GetBytes(SecretBox.KeySize));

        Assert.Throws<InvalidDataException>(() => other.Open("TEST"u8, sealedData, Place));
    }

    [Fact]
    public void AnotherPlace_CannotOpen()
    {
        var sealedData = _box.Seal("TEST"u8, Plain, Place);

        Assert.Throws<InvalidDataException>(() => _box.Open("TEST"u8, sealedData, [9]));
    }

    [Fact]
    public void ChangedData_CannotOpen()
    {
        var sealedData = _box.Seal("TEST"u8, Plain, Place);
        sealedData[^1] ^= 1;

        Assert.Throws<InvalidDataException>(() => _box.Open("TEST"u8, sealedData, Place));
    }

    [Fact]
    public void AnotherMagic_IsRefused()
    {
        var sealedData = _box.Seal("TEST"u8, Plain, Place);

        Assert.Throws<InvalidDataException>(() => _box.Open("OTHR"u8, sealedData, Place));
    }

    [Fact]
    public void TooShortData_IsRefused() =>
        Assert.Throws<InvalidDataException>(() => _box.Open("TEST"u8, "TEST"u8.ToArray(), Place));

    [Fact]
    public void AHeaderShorterThanAMagic_IsAnError()
    {
        Assert.Throws<ArgumentException>(() => _box.Seal("KF"u8, Plain, Place));
        Assert.Throws<ArgumentException>(() => _box.Open("X"u8, [], Place));
    }

    [Fact]
    public void TheBytesAfterTheMagic_AreKeptPlain_AndAuthenticated()
    {
        byte[] header = [.. "TEST"u8, 0, 0, 0, 7];
        var sealedData = _box.Seal(header, Plain, Place);

        Assert.Equal(header, sealedData[..8]);
        Assert.Equal(Plain, _box.Open(header, sealedData, Place));
        sealedData[7] = 8;
        byte[] changed = [.. "TEST"u8, 0, 0, 0, 8];
        Assert.Throws<InvalidDataException>(() => _box.Open(changed, sealedData, Place));
    }

    [Fact]
    public void AKeyOfTheWrongSize_IsAnError() =>
        Assert.Throws<ArgumentException>(() => new SecretBox(new byte[16]));
}
