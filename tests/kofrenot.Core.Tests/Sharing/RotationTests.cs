using Kofrenot.Core.Sharing;

namespace Kofrenot.Core.Tests.Sharing;

public sealed class RotationTests : IDisposable
{
    private const int Iterations = 10;
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private readonly TempFolder _temp = new();
    private readonly (Identity Identity, UnlockedIdentity Unlocked, RecoveryKey Recovery) _owner = Identity.Create("owner", Iterations);
    private readonly (Identity Identity, UnlockedIdentity Unlocked, RecoveryKey Recovery) _staying = Identity.Create("staying", Iterations);
    private readonly (Identity Identity, UnlockedIdentity Unlocked, RecoveryKey Recovery) _leaving = Identity.Create("leaving", Iterations);
    private readonly Guid _stayingAccount = Guid.NewGuid();
    private readonly Guid _leavingAccount = Guid.NewGuid();

    private string Folder => _temp.Sub("vault");

    [Fact]
    public void ARemovedMember_ReadsTheOldFiles_NotTheNext_TheOthersReadBoth()
    {
        var (access, vault) = SharedVault();
        AddNode(vault, "before");

        var (rotated, removal) = VaultSharing.Remove(Folder, access, vault.Graph, [_leavingAccount], OwnerKey, Now);
        vault.UseKeys(rotated.Keys);
        vault.Append(removal);
        AddNode(vault, "after");

        Assert.Equal(1, rotated.Keys.CurrentEpoch);
        Assert.Equal(2, vault.Graph.Members.Count);
        Assert.DoesNotContain(vault.Graph.Members, member => member.Account == _leavingAccount);
        Assert.Equal(0, Unlock(_leaving.Unlocked).Keys.CurrentEpoch);
        Assert.Single(Vault.Open(Folder, access.Header, Unlock(_leaving.Unlocked).Keys).Graph.Nodes);
        Assert.Equal(2, Vault.Open(Folder, access.Header, Unlock(_staying.Unlocked).Keys).Graph.Nodes.Count);
        Assert.Equal(2, Vault.Open(Folder, access.Header, Unlock(_owner.Unlocked).Keys).Graph.Nodes.Count);
    }

    [Fact]
    public void TwoOwnerDevices_RotatingAtOnce_WriteTheSameKey()
    {
        var (access, vault) = SharedVault();

        var first = VaultKeys.Rotate(Folder, access, [(_owner.Identity.Id, _owner.Identity.PublicKey)]);
        var otherDevice = VaultKeys.Rotate(_temp.Sub("other"), access with { }, [(_owner.Identity.Id, _owner.Identity.PublicKey)]);

        Assert.Equal(first.Keys.CurrentKey, otherDevice.Keys.CurrentKey);
        Assert.NotNull(vault);
    }

    [Fact]
    public void AnAccessBehind_OrWithoutIdentity_CannotRotate()
    {
        var (access, _) = SharedVault();
        VaultKeys.Rotate(Folder, access, [(_owner.Identity.Id, _owner.Identity.PublicKey)]);

        Assert.Throws<InvalidOperationException>(() => VaultKeys.Rotate(Folder, access, []));
        Assert.Throws<InvalidOperationException>(() => VaultKeys.Rotate(_temp.Sub("empty"), access with { Identity = null }, []));
    }

    [Fact]
    public void MembersGoneFromTheServer_AreFound_NeverTheOwner()
    {
        var (_, vault) = SharedVault();

        var gone = VaultSharing.Gone(vault.Graph, [new StoreMember(_stayingAccount, StoreRole.Writer)]);

        Assert.Equal([_leavingAccount], gone.Select(member => member.Account));
    }

    [Fact]
    public void FilesWrittenUnderTheOldKey_AreSealedAgain_AndStillRead()
    {
        var (access, vault) = SharedVault();
        var node = AddNode(vault, "written offline");
        var content = Guid.NewGuid();
        vault.Save(new NoteEdited(content, Now, node.Id, []), "text");
        var dataset = new DatasetEdited(Guid.NewGuid(), Now, node.Id, DatasetFiles.Describe(WriteFile(new byte[100])), []);
        vault.Save(dataset, () => new MemoryStream(new byte[100]));
        var rotated = VaultKeys.Rotate(Folder, access, [(_owner.Identity.Id, _owner.Identity.PublicKey)]);
        vault.UseKeys(rotated.Keys);
        var paths = Directory.GetFiles(Folder, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(Folder, file).Replace('\\', '/'))
            .Append("changes/missing.kfc")
            .ToList();

        var resealed = vault.Reseal(paths);

        Assert.Equal(Directory.GetFiles(Folder, "*.kf?", SearchOption.AllDirectories).Length - Directory.GetFiles(Folder, "*.kfw", SearchOption.AllDirectories).Length, resealed);
        Assert.Equal(0, vault.Reseal(paths));
        var reopened = Vault.Open(Folder, access.Header, rotated.Keys);
        Assert.Equal("text", reopened.Notes.Read(content));
        Assert.Single(reopened.Graph.Nodes);
        Assert.Equal(100, ReadAll(reopened.Datasets.OpenRead(dataset.Id)).Length);
    }

    [Fact]
    public void TheEpochOfASealedFile_IsRead_OrNoneForOthers()
    {
        Assert.Equal(7, SealKind.EpochOf([.. "KFN2"u8, 0, 0, 0, 7]));
        Assert.Null(SealKind.EpochOf("KFN1abcd"u8));
        Assert.Null(SealKind.EpochOf("{}"u8));
        Assert.Null(SealKind.EpochOf([.. "KFN2"u8, 0xFF, 0xFF, 0xFF, 0xFF]));
    }

    public void Dispose() => _temp.Dispose();

    private AccountKey OwnerKey => new(_owner.Identity.Id, _owner.Identity.PublicKey);

    // Shared by its owner with two members: one staying, one leaving.
    private (VaultAccess Access, Vault Vault) SharedVault()
    {
        var access = VaultFolder.Create(Folder, _owner.Identity, _owner.Unlocked);
        var vault = Vault.Open(Folder, access.Header, access.Keys);
        var owner = new VaultMember(Guid.NewGuid(), _owner.Identity.Id, _owner.Identity.PublicKey, "ada@example.com", MemberRole.Owner);
        Share(access, vault, owner, new ShareCandidate(_stayingAccount, "bob@example.com", _staying.Identity.Id, _staying.Identity.PublicKey));
        Share(access, vault, owner, new ShareCandidate(_leavingAccount, "eve@example.com", _leaving.Identity.Id, _leaving.Identity.PublicKey));
        return (access, vault);
    }

    private void Share(VaultAccess access, Vault vault, VaultMember owner, ShareCandidate member)
    {
        foreach (var change in VaultSharing.Share(Folder, access, vault.Graph, owner, member, MemberRole.Writer, Now))
        {
            vault.Append(change);
        }
    }

    private VaultAccess Unlock(UnlockedIdentity identity) => VaultKeys.Unlock(Folder, VaultHeader.Read(VaultFolder.HeaderOf(Folder)), identity)!;

    private static Node AddNode(Vault vault, string name)
    {
        var node = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, name, Now);
        vault.Append(new NodeAdded(Guid.NewGuid(), Now, node));
        return node;
    }

    private string WriteFile(byte[] content)
    {
        var path = _temp.Sub("source.bin");
        File.WriteAllBytes(path, content);
        return path;
    }

    private static byte[] ReadAll(Stream stream)
    {
        using (stream)
        {
            var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }
    }
}
