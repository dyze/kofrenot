using System.Text;

namespace Kofrenot.Core.Tests.Storage;

public sealed class VaultFolderTests : IDisposable
{
    private const int Iterations = 10;

    private readonly TempFolder _temp = new();

    private string Folder => _temp.Sub("db");

    [Fact]
    public void AMissingFolder_IsEmpty() =>
        Assert.Equal(FolderKind.Empty, VaultFolder.Inspect(Folder));

    [Fact]
    public void AnEmptyFolder_IsEmpty()
    {
        Directory.CreateDirectory(Folder);

        Assert.Equal(FolderKind.Empty, VaultFolder.Inspect(Folder));
    }

    [Fact]
    public void AFolderWithOtherFiles_IsOther()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "notes.txt"), "mine");

        Assert.Equal(FolderKind.Other, VaultFolder.Inspect(Folder));
    }

    [Fact]
    public void ACreatedVault_IsAVault_AndUnlocksWithItsPassPhrase_OrItsRecoveryKey()
    {
        var (access, recovery) = VaultFolder.Create(Folder, "secret", Iterations);

        Assert.Equal(FolderKind.Vault, VaultFolder.Inspect(Folder));
        Assert.True(Directory.Exists(Path.Combine(Folder, VaultFolder.ChangesFolder)));
        var read = VaultHeader.Read(VaultFolder.HeaderOf(Folder));
        Assert.Equal(access.Header.Id, read.Id);
        Assert.Equal(VaultFormat.Current, read.Format);
        Assert.Null(read.Key);
        Assert.Equal(access.Keys.CurrentKey, VaultKeys.Unlock(Folder, read, "secret")?.Keys.CurrentKey);
        Assert.Equal(access.Keys.CurrentKey, VaultKeys.Unlock(Folder, read, recovery)?.Keys.CurrentKey);
        Assert.Null(VaultKeys.Unlock(Folder, read, "wrong"));
    }

    [Fact]
    public void AHeaderWithoutKey_IsRead_FromFormat6Only()
    {
        const string Id = "\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\"";

        Assert.Null(VaultHeader.Read(WriteHeader($"{{\"format\":6,{Id}}}")).Unlock("secret"));
        Assert.Throws<InvalidDataException>(() => VaultHeader.Read(WriteHeader($"{{\"format\":5,{Id}}}")));
    }

    [Fact]
    public void TheHeader_HoldsNoSecret()
    {
        VaultFolder.Create(Folder, "my secret phrase", Iterations);

        Assert.DoesNotContain("my secret phrase", File.ReadAllText(VaultFolder.HeaderOf(Folder)));
    }

    [Fact]
    public void AVault_IsNotCreatedInAFolderInUse()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "notes.txt"), "mine");

        Assert.Throws<IOException>(() => VaultFolder.Create(Folder, "secret", Iterations));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("null")]
    [InlineData("{\"format\":1}")]
    [InlineData("{\"format\":0,\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"key\":{\"salt\":\"AA==\",\"iterations\":1,\"sealed\":\"AA==\"}}")]
    [InlineData("{\"format\":1,\"key\":{\"salt\":\"AA==\",\"iterations\":1,\"sealed\":\"AA==\"}}")]
    [InlineData("{\"format\":1,\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"key\":{\"iterations\":1,\"sealed\":\"AA==\"}}")]
    [InlineData("{\"format\":1,\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"key\":{\"salt\":\"AA==\",\"iterations\":0,\"sealed\":\"AA==\"}}")]
    [InlineData("{\"format\":1,\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"key\":{\"salt\":\"AA==\",\"iterations\":1}}")]
    public void ADamagedHeader_IsRefused(string json)
    {
        var path = WriteHeader(json);

        Assert.Throws<InvalidDataException>(() => VaultHeader.Read(path));
    }

    [Fact]
    public void AHeaderOfANewerFormat_IsRefused()
    {
        var path = WriteHeader("{\"format\":99,\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\"}");

        var refused = Assert.Throws<NewerVaultFormatException>(() => VaultHeader.Read(path));
        Assert.Equal(99, refused.Format);
        Assert.Contains($"reads up to format {VaultFormat.Current}", refused.Message);
    }

    [Fact]
    public void TheSize_CountsEveryFile_InEverySubFolder()
    {
        VaultFolder.Create(Folder, "secret phrase", Iterations);
        var created = Directory.GetFiles(Folder, "*", SearchOption.AllDirectories).Select(file => new FileInfo(file).Length).ToList();
        var nested = Path.Combine(Folder, VaultFolder.ContentFolder, "3f");
        Directory.CreateDirectory(nested);
        File.WriteAllBytes(Path.Combine(nested, "a.kfd"), new byte[2000]);
        File.WriteAllBytes(Path.Combine(Folder, VaultFolder.ChangesFolder, "b.kfc"), new byte[48]);

        var size = VaultFolder.SizeOf(Folder);

        Assert.Equal(3, created.Count);
        Assert.Equal(new FolderSize(created.Count + 2, created.Sum() + 2048), size);
    }

    [Fact]
    public void AMissingFolder_HasNoSize() =>
        Assert.Equal(new FolderSize(0, 0), VaultFolder.SizeOf(Folder));

    [Theory]
    [InlineData(1, 512, "512 bytes in 1 file")]
    [InlineData(87, 4404019, "4.2 MB in 87 files")]
    public void TheSize_IsShown(int files, long bytes, string shown) =>
        Assert.Equal(shown, new FolderSize(files, bytes).Describe(System.Globalization.CultureInfo.InvariantCulture));

    public void Dispose() => _temp.Dispose();

    private string WriteHeader(string json)
    {
        Directory.CreateDirectory(Folder);
        var path = VaultFolder.HeaderOf(Folder);
        File.WriteAllText(path, json, Encoding.UTF8);
        return path;
    }
}
