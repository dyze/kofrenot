using System.Security.Cryptography;

namespace Kofrenot.Core.Tests.Storage;

public sealed class EpochKeysTests
{
    private static readonly byte[] VaultKey = RandomNumberGenerator.GetBytes(SecretBox.KeySize);
    private static readonly Guid Vault = Guid.NewGuid();

    private readonly (Identity Identity, UnlockedIdentity Unlocked, RecoveryKey Recovery) _member = Identity.Create("secret", iterations: 10);

    [Fact]
    public void AWrappedKey_IsUnwrapped_ByItsIdentity()
    {
        var wrapped = Wrap(epoch: 3);

        Assert.Equal(VaultKey, EpochKeys.Unwrap(wrapped, _member.Unlocked, Vault, 3));
        Assert.Equal("KFW1"u8.ToArray(), wrapped[..4]);
    }

    [Fact]
    public void AnotherIdentity_Vault_OrEpoch_CannotUnwrap()
    {
        var wrapped = Wrap(epoch: 0);
        var (_, stranger, _) = Identity.Create("secret", iterations: 10);

        Assert.Throws<InvalidDataException>(() => EpochKeys.Unwrap(wrapped, stranger with { Id = _member.Unlocked.Id }, Vault, 0));
        Assert.Throws<InvalidDataException>(() => EpochKeys.Unwrap(wrapped, _member.Unlocked, Guid.NewGuid(), 0));
        Assert.Throws<InvalidDataException>(() => EpochKeys.Unwrap(wrapped, _member.Unlocked, Vault, 1));
    }

    [Fact]
    public void ADamagedWrap_IsRefused()
    {
        var wrapped = Wrap(epoch: 0);
        var badPoint = wrapped.ToArray();
        badPoint[10] ^= 0xFF;

        Assert.Throws<InvalidDataException>(() => EpochKeys.Unwrap(wrapped[..20], _member.Unlocked, Vault, 0));
        Assert.Throws<InvalidDataException>(() => EpochKeys.Unwrap([.. "KFX1"u8, .. wrapped[4..]], _member.Unlocked, Vault, 0));
        Assert.Throws<InvalidDataException>(() => EpochKeys.Unwrap(badPoint, _member.Unlocked, Vault, 0));
    }

    [Fact]
    public void WrappingTwice_GivesDifferentData() =>
        Assert.NotEqual(Wrap(epoch: 0), Wrap(epoch: 0));

    private byte[] Wrap(int epoch) => EpochKeys.Wrap(VaultKey, _member.Identity.PublicKey, Vault, epoch, _member.Identity.Id);
}
