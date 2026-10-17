using System.Security.Cryptography;
using System.Text;

namespace Kofrenot.Core.Tests.Storage;

public sealed class NoteFilesTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly RingLog _log = new();
    private readonly SecretBox _box = new(RandomNumberGenerator.GetBytes(SecretBox.KeySize));

    private string Folder => _temp.Sub("vault");

    [Fact]
    public void AWrittenContent_IsReadBack_FromAnEncryptedFileOfItsOwn()
    {
        var version = Guid.NewGuid();

        Files().Write(version, "# Ideas\nÉté");

        Assert.Equal("# Ideas\nÉté", Files().Read(version));
        var file = Assert.Single(Directory.GetFiles(Path.Combine(Folder, VaultFolder.ContentFolder), "*", SearchOption.AllDirectories));
        Assert.Equal(version.ToString("N") + VaultFolder.ContentExtension, Path.GetFileName(file));
        Assert.DoesNotContain("Ideas", Encoding.UTF8.GetString(File.ReadAllBytes(file)));
    }

    [Fact]
    public void AContentNotThere_IsNull() =>
        Assert.Null(Files().Read(Guid.NewGuid()));

    [Fact]
    public void AContentWrittenTwice_IsRefused()
    {
        var version = Guid.NewGuid();
        Files().Write(version, "one");

        Assert.ThrowsAny<IOException>(() => Files().Write(version, "two"));
    }

    [Fact]
    public void AContentUnderAnotherId_OrDamaged_IsNull_AndReported()
    {
        var (version, other) = (Guid.NewGuid(), Guid.NewGuid());
        Files().Write(version, "secret");
        var otherPath = Path.Combine(Folder, VaultFolder.ContentFolder, other.ToString("N")[..2], other.ToString("N") + VaultFolder.ContentExtension);
        Directory.CreateDirectory(Path.GetDirectoryName(otherPath)!);
        File.Copy(Directory.GetFiles(Path.Combine(Folder, VaultFolder.ContentFolder), "*", SearchOption.AllDirectories).Single(), otherPath);

        Assert.Null(Files().Read(other));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("cannot be read"));
    }

    public void Dispose() => _temp.Dispose();

    private NoteFiles Files() => new(Folder, _box, _log);
}
