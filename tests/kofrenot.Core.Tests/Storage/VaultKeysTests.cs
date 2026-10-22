namespace Kofrenot.Core.Tests.Storage;

public sealed class VaultKeysTests : IDisposable
{
    private const int Iterations = 10;

    private readonly TempFolder _temp = new();
    private readonly RingLog _log = new();

    private string Folder => _temp.Sub("vault");

    private string Keys => Path.Combine(Folder, VaultFolder.KeysFolder);

    [Fact]
    public void ANewVault_HasAnIdentity_AndItsKeyOfEpoch0()
    {
        var (access, _) = VaultFolder.Create(Folder, "secret", Iterations);

        Assert.True(VaultKeys.HasIdentities(Folder));
        Assert.NotNull(access.Identity);
        Assert.Single(Directory.GetFiles(Path.Combine(Keys, "identities"), "*.json"));
        Assert.Single(Directory.GetFiles(Path.Combine(Keys, "epoch-0"), access.Identity.Id.ToString("N") + ".*.kfw"));
    }

    [Fact]
    public void ARememberedIdentity_UnlocksTheVault()
    {
        var (access, _) = VaultFolder.Create(Folder, "secret", Iterations);

        var unlocked = VaultKeys.Unlock(Folder, access.Header, access.Identity!);

        Assert.Equal(access.Keys.CurrentKey, unlocked?.Keys.CurrentKey);
        Assert.Null(VaultKeys.Unlock(Folder, access.Header, Identity.Create("secret", Iterations).Unlocked));
    }

    [Fact]
    public void AFormat5Vault_UnlocksWithItsHeader_ThenUpgrades_WithoutRewritingAFile()
    {
        var header = Format5Vault("secret", out var masterKey);
        var before = File.ReadAllBytes(VaultFolder.HeaderOf(Folder));

        Assert.False(VaultKeys.HasIdentities(Folder));
        var legacy = VaultKeys.Unlock(Folder, header, "secret");
        Assert.Null(legacy?.Identity);
        Assert.Equal(masterKey, legacy?.Keys.KeyOf(0));

        Assert.Null(VaultKeys.Upgrade(Folder, header, "wrong", Iterations));
        var (upgraded, recovery) = VaultKeys.Upgrade(Folder, header, "secret", Iterations)!.Value;

        Assert.Equal(masterKey, upgraded.Keys.KeyOf(0));
        Assert.Equal(before, File.ReadAllBytes(VaultFolder.HeaderOf(Folder)));
        Assert.NotNull(VaultKeys.Unlock(Folder, header, "secret")?.Identity);
        Assert.Equal(masterKey, VaultKeys.Unlock(Folder, header, recovery)?.Keys.KeyOf(0));
    }

    [Fact]
    public void ANewPassPhrase_ReplacesTheOld_AndTheHeaderLosesItsKey()
    {
        var header = Format5Vault("secret", out var masterKey);
        var (access, recovery) = VaultKeys.Upgrade(Folder, header, "secret", Iterations)!.Value;

        var changed = VaultKeys.ChangePassPhrase(Folder, access, "new secret", Iterations);

        var read = VaultHeader.Read(VaultFolder.HeaderOf(Folder));
        Assert.Null(read.Key);
        Assert.Equal(VaultHeader.KeysApart, read.Format);
        Assert.Equal(read, changed.Header);
        Assert.Null(VaultKeys.Unlock(Folder, read, "secret"));
        Assert.Equal(masterKey, VaultKeys.Unlock(Folder, read, "new secret")?.Keys.KeyOf(0));
        Assert.NotNull(VaultKeys.Unlock(Folder, read, recovery));
    }

    [Fact]
    public void ChangingThePassPhraseOfAFormat6Vault_KeepsItsHeader()
    {
        var (access, _) = VaultFolder.Create(Folder, "secret", Iterations);
        var before = File.ReadAllBytes(VaultFolder.HeaderOf(Folder));

        Assert.Same(access, VaultKeys.ChangePassPhrase(Folder, access, "new secret", Iterations));

        Assert.Equal(before, File.ReadAllBytes(VaultFolder.HeaderOf(Folder)));
        Assert.NotNull(VaultKeys.Unlock(Folder, access.Header, "new secret"));
    }

    [Fact]
    public void AVaultNotUpgraded_HasNoIdentityToChange()
    {
        var header = Format5Vault("secret", out _);
        var legacy = VaultKeys.Unlock(Folder, header, "secret")!;

        Assert.Throws<InvalidOperationException>(() => VaultKeys.ChangePassPhrase(Folder, legacy, "new", Iterations));
    }

    [Fact]
    public void ADamagedIdentity_IsSkipped_AndLogged()
    {
        var (access, _) = VaultFolder.Create(Folder, "secret", Iterations);
        File.WriteAllText(Path.Combine(Keys, "identities", "broken.json"), "{ not json");

        Assert.NotNull(VaultKeys.Unlock(Folder, access.Header, "secret", _log));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("broken"));
    }

    [Fact]
    public void AnIdentityWithoutKey_CannotOpenTheVault()
    {
        var (access, recovery) = VaultFolder.Create(Folder, "secret", Iterations);
        Directory.Delete(Path.Combine(Keys, "epoch-0"), recursive: true);

        Assert.Throws<InvalidDataException>(() => VaultKeys.Unlock(Folder, access.Header, "secret"));
        Assert.Throws<InvalidDataException>(() => VaultKeys.Unlock(Folder, access.Header, recovery));
    }

    [Fact]
    public void ADamagedKey_IsSkipped_AndLogged_AndOtherEpochsStay()
    {
        var (access, _) = VaultFolder.Create(Folder, "secret", Iterations);
        var name = access.Identity!.Id.ToString("N") + ".kfw";
        Directory.CreateDirectory(Path.Combine(Keys, "epoch-1"));
        File.WriteAllBytes(Path.Combine(Keys, "epoch-1", name), [1, 2, 3]);
        Directory.CreateDirectory(Path.Combine(Keys, "epoch-x"));

        var unlocked = VaultKeys.Unlock(Folder, access.Header, access.Identity, _log);

        Assert.Equal(0, unlocked?.Keys.CurrentEpoch);
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("epoch 1"));
    }

    [Fact]
    public void AnotherRecoveryKey_DoesNotUnlock()
    {
        var (access, _) = VaultFolder.Create(Folder, "secret", Iterations);

        Assert.Null(VaultKeys.Unlock(Folder, access.Header, RecoveryKey.New()));
    }

    public void Dispose() => _temp.Dispose();

    // A vault as kofrenot 0.6 created it: its master key in its header, no keys/ folder.
    private VaultHeader Format5Vault(string passPhrase, out byte[] masterKey)
    {
        var id = Guid.NewGuid();
        (var wrapped, masterKey) = KeyWrap.Create(passPhrase, id.ToByteArray(), Iterations);
        var header = new VaultHeader(5, id, wrapped);
        Directory.CreateDirectory(Path.Combine(Folder, VaultFolder.ChangesFolder));
        File.WriteAllText(VaultFolder.HeaderOf(Folder), $"{{\"format\":5,\"id\":\"{id}\",\"key\":{{\"salt\":\"{Convert.ToBase64String(wrapped.Salt)}\",\"iterations\":{Iterations},\"sealed\":\"{Convert.ToBase64String(wrapped.Sealed)}\"}}}}");
        Assert.Equal(id, VaultHeader.Read(VaultFolder.HeaderOf(Folder)).Id);
        return header;
    }
}
