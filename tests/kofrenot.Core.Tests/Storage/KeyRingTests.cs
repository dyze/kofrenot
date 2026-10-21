using System.Security.Cryptography;

namespace Kofrenot.Core.Tests.Storage;

public sealed class KeyRingTests
{
    private static readonly byte[] First = RandomNumberGenerator.GetBytes(SecretBox.KeySize);
    private static readonly byte[] Second = RandomNumberGenerator.GetBytes(SecretBox.KeySize);

    [Fact]
    public void ARingOfOneKey_IsOfEpoch0()
    {
        var keys = KeyRing.Of(First);

        Assert.Equal(0, keys.CurrentEpoch);
        Assert.Equal(First, keys.CurrentKey);
        Assert.Equal(First, keys.KeyOf(0));
    }

    [Fact]
    public void TheLatestEpoch_IsTheCurrentOne_AndEachKeepsItsKey()
    {
        var keys = new KeyRing(new Dictionary<int, byte[]> { [3] = Second, [0] = First });

        Assert.Equal(3, keys.CurrentEpoch);
        Assert.Equal(Second, keys.CurrentKey);
        Assert.Equal(First, keys.KeyOf(0));
    }

    [Fact]
    public void AnEpochWithoutKey_CannotBeOpened() =>
        Assert.Throws<InvalidDataException>(() => KeyRing.Of(First).KeyOf(1));

    [Fact]
    public void NoKey_ANegativeEpoch_OrAKeyOfTheWrongSize_IsAnError()
    {
        Assert.Throws<ArgumentException>(() => new KeyRing(new Dictionary<int, byte[]>()));
        Assert.Throws<ArgumentException>(() => new KeyRing(new Dictionary<int, byte[]> { [-1] = First }));
        Assert.Throws<ArgumentException>(() => KeyRing.Of(new byte[16]));
    }
}
