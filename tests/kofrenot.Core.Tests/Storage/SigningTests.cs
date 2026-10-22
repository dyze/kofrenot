namespace Kofrenot.Core.Tests.Storage;

public sealed class SigningTests
{
    private static readonly byte[] Data = [1, 2, 3];

    private readonly UnlockedIdentity _identity = Identity.Create("secret", iterations: 10).Unlocked;

    [Fact]
    public void ASignature_IsVerified_WithTheIdentitysSigningKey_TheSameEachTime()
    {
        var signature = Signing.Sign(_identity, Data);

        Assert.Equal(Signing.SignatureSize, signature.Length);
        Assert.True(Signing.Verify(Signing.PublicKeyOf(_identity), Data, signature));
        Assert.Equal(Signing.PublicKeyOf(_identity), Signing.PublicKeyOf(_identity with { }));
    }

    [Fact]
    public void OtherData_AnotherKey_OrADamagedSignature_DoNotVerify()
    {
        var signature = Signing.Sign(_identity, Data);
        var other = Identity.Create("secret", iterations: 10).Unlocked;

        Assert.False(Signing.Verify(Signing.PublicKeyOf(_identity), [9], signature));
        Assert.False(Signing.Verify(Signing.PublicKeyOf(other), Data, signature));
        Assert.False(Signing.Verify(Signing.PublicKeyOf(_identity), Data, signature[..10]));
        Assert.False(Signing.Verify([1, 2, 3], Data, signature));
    }

    [Fact]
    public void TheSigningKey_IsNotTheEncryptionKey()
    {
        var (identity, unlocked, _) = Identity.Create("secret", iterations: 10);

        Assert.NotEqual(identity.PublicKey, Signing.PublicKeyOf(unlocked));
    }
}
