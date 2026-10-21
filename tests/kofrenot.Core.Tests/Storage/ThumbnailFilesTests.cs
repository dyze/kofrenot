using System.Security.Cryptography;

namespace Kofrenot.Core.Tests.Storage;

public sealed class ThumbnailFilesTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly RingLog _log = new();
    private readonly KeyedBox _box = new(KeyRing.Of(RandomNumberGenerator.GetBytes(SecretBox.KeySize)));

    private string ContentFolder => Path.Combine(_temp.Sub("vault"), VaultFolder.ContentFolder);

    [Fact]
    public void AWrittenThumbnail_IsReadBack_FromAnEncryptedFileOfItsOwn()
    {
        var version = Guid.NewGuid();
        byte[] png = [0x89, (byte)'P', (byte)'N', (byte)'G', 1, 2, 3];

        Files().Write(version, png);

        Assert.Equal(png, Files().Read(version));
        var file = Assert.Single(Directory.GetFiles(ContentFolder, "*", SearchOption.AllDirectories));
        Assert.Equal(version.ToString("N") + VaultFolder.ThumbnailExtension, Path.GetFileName(file));
        Assert.DoesNotContain("PNG", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(file)));
    }

    [Fact]
    public void AThumbnailNotThere_IsNull() =>
        Assert.Null(Files().Read(Guid.NewGuid()));

    [Fact]
    public void AThumbnailWrittenTwice_IsRefused()
    {
        var version = Guid.NewGuid();
        Files().Write(version, [1]);

        Assert.ThrowsAny<IOException>(() => Files().Write(version, [2]));
    }

    [Fact]
    public void ADamagedThumbnail_IsNull_AndReported()
    {
        var version = Guid.NewGuid();
        Files().Write(version, [1, 2, 3]);
        var file = Directory.GetFiles(ContentFolder, "*", SearchOption.AllDirectories).Single();
        var bytes = File.ReadAllBytes(file);
        bytes[^1] ^= 1;
        File.WriteAllBytes(file, bytes);

        Assert.Null(Files().Read(version));
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("cannot be read"));
    }

    public void Dispose() => _temp.Dispose();

    private ThumbnailFiles Files() => new(_temp.Sub("vault"), _box, _log);
}
