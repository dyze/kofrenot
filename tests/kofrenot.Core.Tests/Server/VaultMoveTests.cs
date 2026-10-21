namespace Kofrenot.Core.Tests.Server;

public sealed class VaultMoveTests : IDisposable
{
    private readonly TempFolder _temp = new();

    private string Folder => _temp.Sub("vault");

    private string Mirror => _temp.Sub("mirror");

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void TheVaultsFiles_AreCopiedAsTheyAre_AndTheFolderKept()
    {
        var (header, key, _) = VaultFolder.Create(Folder, "secret", iterations: 10).Access;
        var vault = Vault.Open(Folder, header, key);
        vault.Append(new NodeAdded(Guid.NewGuid(), DateTimeOffset.UtcNow, new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Dune", DateTimeOffset.UtcNow)));
        File.WriteAllText(Path.Combine(Folder, "changes", "half.kfc.tmp"), "x");
        File.WriteAllText(Path.Combine(Folder, "desktop ini"), "x");

        var copied = VaultMove.CopyToMirror(Folder, Mirror);

        Assert.Equal(4, copied);
        Assert.Equal(header.Id, Vault.Open(Mirror, VaultHeader.Read(VaultFolder.HeaderOf(Mirror)), key).Id);
        Assert.Single(Vault.Open(Mirror, header, key).Graph.Nodes);
        Assert.Equal(6, Directory.GetFiles(Folder, "*", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public void OnlyAVault_IsMoved()
    {
        Directory.CreateDirectory(Folder);

        Assert.Throws<IOException>(() => VaultMove.CopyToMirror(Folder, Mirror));
    }

    [Fact]
    public void AVault_IsMovedIntoAnEmptyMirrorOnly()
    {
        VaultFolder.Create(Folder, "secret", iterations: 10);
        Directory.CreateDirectory(Mirror);
        File.WriteAllText(Path.Combine(Mirror, "other"), "x");

        Assert.Throws<IOException>(() => VaultMove.CopyToMirror(Folder, Mirror));
    }
}
