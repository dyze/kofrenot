namespace Kofrenot.Core.Tests.Storage;

public sealed class IdentityTests : IDisposable
{
    private const int Iterations = 10;

    private readonly TempFolder _temp = new();

    [Fact]
    public void ANewIdentity_UnlocksWithItsPassPhrase_OrItsRecoveryKey()
    {
        var (identity, unlocked, recovery) = Identity.Create("secret", Iterations);

        Assert.Equal(unlocked.PrivateKey, identity.Unlock("secret")?.PrivateKey);
        Assert.Equal(unlocked.PrivateKey, identity.Unlock(recovery)?.PrivateKey);
        Assert.Equal(identity.Id, unlocked.Id);
    }

    [Fact]
    public void AWrongPassPhrase_OrAnotherRecoveryKey_DoesNotUnlock()
    {
        var (identity, _, _) = Identity.Create("secret", Iterations);

        Assert.Null(identity.Unlock("wrong"));
        Assert.Null(identity.Unlock(RecoveryKey.New()));
    }

    [Fact]
    public void AnotherPassPhrase_ReplacesTheFirst_AndTheRecoveryKeyStillOpens()
    {
        var (identity, unlocked, recovery) = Identity.Create("secret", Iterations);

        var changed = identity.WithPassPhrase(unlocked, "new secret", Iterations);

        Assert.Null(changed.Unlock("secret"));
        Assert.Equal(unlocked.PrivateKey, changed.Unlock("new secret")?.PrivateKey);
        Assert.NotNull(changed.Unlock(recovery));
        Assert.Equal(identity.PublicKey, changed.PublicKey);
    }

    [Fact]
    public void AnotherIdentity_CannotSetThePassPhrase()
    {
        var (identity, _, _) = Identity.Create("secret", Iterations);
        var (_, other, _) = Identity.Create("secret", Iterations);

        Assert.Throws<ArgumentException>(() => identity.WithPassPhrase(other, "new", Iterations));
    }

    [Fact]
    public void AnIdentity_IsWrittenAndRead_WithoutItsSecrets()
    {
        var (access, recovery) = VaultFolder.Create(_temp.Sub("vault"), "my secret phrase", Iterations);
        var path = Path.Combine(_temp.Sub("vault"), VaultFolder.KeysFolder, "identities", access.Identity!.Id.ToString("N") + ".json");

        var read = Identity.Read(path);

        Assert.Equal(access.Identity.Id, read.Id);
        Assert.NotNull(read.Unlock("my secret phrase"));
        var text = File.ReadAllText(path);
        Assert.DoesNotContain("my secret phrase", text);
        Assert.DoesNotContain(recovery.ToString(), text);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("null")]
    [InlineData("{\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\"}")]
    [InlineData("{\"publicKey\":\"AA==\",\"byRecoveryKey\":\"AA==\",\"byPassPhrase\":{\"salt\":\"AA==\",\"iterations\":1,\"sealed\":\"AA==\"}}")]
    [InlineData("{\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"publicKey\":\"AA==\",\"byRecoveryKey\":\"AA==\",\"byPassPhrase\":{\"salt\":\"AA==\",\"iterations\":0,\"sealed\":\"AA==\"}}")]
    public void ADamagedIdentity_IsRefused(string json)
    {
        Directory.CreateDirectory(_temp.Path);
        var path = _temp.Sub("identity.json");
        File.WriteAllText(path, json);

        Assert.Throws<InvalidDataException>(() => Identity.Read(path));
    }

    public void Dispose() => _temp.Dispose();
}
