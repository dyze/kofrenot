namespace Kofrenot.Core.Tests.Storage;

public sealed class VaultAttachTests : IDisposable
{
    private const int Iterations = 10;

    private readonly TempFolder _temp = new();
    private readonly (Identity Identity, UnlockedIdentity Unlocked, RecoveryKey Recovery) _account = Identity.Create("account phrase", Iterations);

    private string Folder => _temp.Sub("vault");

    [Fact]
    public void AVaultForAnAccount_HasNoIdentityOfItsOwn_AndOpensWithTheAccounts()
    {
        var access = VaultFolder.Create(Folder, _account.Identity, _account.Unlocked);

        Assert.False(VaultKeys.HasIdentities(Folder));
        Assert.True(VaultKeys.IsAttached(Folder, IdOf(access), _account.Identity.Id, AccountSigningKey));
        Assert.Equal(access.Keys.CurrentKey, VaultKeys.Unlock(Folder, access.Header, "account phrase", account: _account.Identity)?.Keys.CurrentKey);
        Assert.NotNull(VaultKeys.Unlock(Folder, access.Header, _account.Recovery, account: _account.Identity));
        Assert.Null(VaultKeys.Unlock(Folder, access.Header, "account phrase"));
    }

    [Fact]
    public void AnAttachedVault_HasANewEpoch_TheOldPassPhraseOpensOnlyTheOldFiles()
    {
        var (own, _) = VaultFolder.Create(Folder, "own phrase", Iterations);

        var attached = VaultKeys.Attach(Folder, own, _account.Identity, _account.Unlocked);

        Assert.Equal(1, attached.Keys.CurrentEpoch);
        Assert.Equal(1, VaultKeys.LatestEpoch(Folder));
        Assert.Equal(_account.Unlocked, attached.Identity);
        Assert.True(VaultKeys.IsAttached(Folder, IdOf(own), _account.Identity.Id, AccountSigningKey));
        Assert.Equal(1, VaultKeys.LatestEpoch(Folder, IdOf(own), AccountSigningKey));
        Assert.False(VaultKeys.IsAttached(Folder, IdOf(own), own.Identity!.Id, AccountSigningKey));
        var byOld = VaultKeys.Unlock(Folder, own.Header, "own phrase")!;
        Assert.False(VaultKeys.IsCurrent(Folder, byOld));
        var byAccount = VaultKeys.Unlock(Folder, own.Header, "account phrase", account: _account.Identity)!;
        Assert.True(VaultKeys.IsCurrent(Folder, byAccount));
        Assert.Equal([0, 1], byAccount.Keys.Epochs);
        Assert.Equal(own.Keys.KeyOf(0), byAccount.Keys.KeyOf(0));
    }

    [Fact]
    public void TwoDevicesAttachingAtOnce_WriteTheSameKey()
    {
        var (own, _) = VaultFolder.Create(Folder, "own phrase", Iterations);
        var otherDevice = _temp.Sub("other");
        CopyFolder(Folder, otherDevice);

        var first = VaultKeys.Attach(Folder, own, _account.Identity, _account.Unlocked);
        var second = VaultKeys.Attach(otherDevice, own, _account.Identity, _account.Unlocked);

        Assert.Equal(first.Keys.CurrentKey, second.Keys.CurrentKey);
    }

    [Fact]
    public void AFormat5Vault_IsAttached_ItsMasterKeyAsEpoch0()
    {
        var header = new VaultHeader(5, Guid.NewGuid());
        var masterKey = System.Security.Cryptography.RandomNumberGenerator.GetBytes(SecretBox.KeySize);
        Directory.CreateDirectory(Folder);
        var legacy = new VaultAccess(header, KeyRing.Of(masterKey), Identity: null);

        Assert.Equal(0, VaultKeys.LatestEpoch(Folder));
        Assert.True(VaultKeys.IsCurrent(Folder, legacy));
        var attached = VaultKeys.Attach(Folder, legacy, _account.Identity, _account.Unlocked);

        Assert.Equal(masterKey, VaultKeys.Unlock(Folder, header, _account.Unlocked)?.Keys.KeyOf(0));
        Assert.Equal(attached.Keys.CurrentKey, VaultKeys.Unlock(Folder, header, _account.Unlocked)?.Keys.CurrentKey);
    }

    [Fact]
    public void AttachingAgain_GivesTheSameKey_ButNotForAnotherIdentity()
    {
        var (own, _) = VaultFolder.Create(Folder, "own phrase", Iterations);
        var first = VaultKeys.Attach(Folder, own, _account.Identity, _account.Unlocked);
        var (_, stranger, _) = Identity.Create("other", Iterations);

        Assert.Equal(first.Keys.CurrentKey, VaultKeys.Attach(Folder, own, _account.Identity, _account.Unlocked).Keys.CurrentKey);
        Assert.Single(Directory.GetFiles(Path.Combine(Folder, VaultFolder.KeysFolder, "epoch-1")));
        Assert.Throws<InvalidOperationException>(() => VaultKeys.Attach(Folder, first with { Keys = KeyRing.Of(first.Keys.KeyOf(0)) }, _account.Identity, _account.Unlocked));
        Assert.Throws<ArgumentException>(() => VaultKeys.Attach(Folder, own, _account.Identity, stranger));
    }

    [Fact]
    public void AnOpenVault_WritesWithTheNewEpoch_AndStillReadsTheOld()
    {
        var (own, _) = VaultFolder.Create(Folder, "own phrase", Iterations);
        var vault = Vault.Open(Folder, own.Header, own.Keys);
        var node = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Dune", DateTimeOffset.UtcNow);
        vault.Append(new NodeAdded(Guid.NewGuid(), node.Created, node));

        var attached = VaultKeys.Attach(Folder, own, _account.Identity, _account.Unlocked);
        vault.UseKeys(attached.Keys);
        var other = node with { Id = Guid.NewGuid(), Name = "Herbert" };
        vault.Append(new NodeAdded(Guid.NewGuid(), other.Created, other));

        Assert.Equal(2, Vault.Open(Folder, own.Header, attached.Keys).Graph.Nodes.Count);
        Assert.Single(Vault.Open(Folder, own.Header, own.Keys).Graph.Nodes);
    }

    private byte[] AccountSigningKey => Signing.PublicKeyOf(_account.Unlocked);

    private static Guid IdOf(VaultAccess access) => access.Header.Id;

    public void Dispose() => _temp.Dispose();

    private static void CopyFolder(string from, string to)
    {
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
