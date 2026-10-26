using System.Security.Cryptography;
using System.Text;

namespace Kofrenot.Core.Tests.Storage;

public sealed class DatasetFilesTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);

    private readonly TempFolder _temp = new();
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(SecretBox.KeySize);

    public DatasetFilesTests() => Directory.CreateDirectory(_temp.Path);

    private string Folder => _temp.Sub("vault");

    private string ContentFolder => Path.Combine(Folder, VaultFolder.ContentFolder);

    [Fact]
    public void AFile_IsDescribed_ByItsName_Size_AndHash()
    {
        var path = Source("survey.csv", "a,b\n1,2\n");

        var file = DatasetFiles.Describe(path);

        Assert.Equal(new DatasetFile("survey.csv", 8, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("a,b\n1,2\n")))), file);
    }

    [Fact]
    public void AWrittenFile_IsExported_FromAnEncryptedFileOfItsOwn()
    {
        var source = Source("survey.csv", "secret,values");
        var version = Version(source);

        Write(version, source);

        Assert.True(Files().Exists(version.Id));
        var sealedFile = Assert.Single(Directory.GetFiles(ContentFolder, "*", SearchOption.AllDirectories));
        Assert.Equal(version.Id.ToString("N") + VaultFolder.DatasetExtension, Path.GetFileName(sealedFile));
        Assert.DoesNotContain("secret", Encoding.UTF8.GetString(File.ReadAllBytes(sealedFile)));

        var target = _temp.Sub("out.csv");
        Files().Export(version.Id, target);
        Assert.Equal("secret,values", File.ReadAllText(target));
        Assert.False(File.Exists(target + ".tmp"));
    }

    [Fact]
    public void AFile_IsExported_ToAStream_LeftOpen()
    {
        var source = Source("survey.csv", "secret,values");
        var version = Version(source);
        Write(version, source);
        using var target = new MemoryStream();

        Files().Export(version.Id, target);

        Assert.Equal("secret,values", Encoding.UTF8.GetString(target.ToArray()));
        Assert.True(target.CanWrite);
    }

    [Fact]
    public void AnExport_ReplacesTheTarget()
    {
        var source = Source("a.txt", "new");
        var version = Version(source);
        Write(version, source);
        var target = Source("old.txt", "old content");

        Files().Export(version.Id, target);

        Assert.Equal("new", File.ReadAllText(target));
    }

    [Fact]
    public void AFileChangedSinceDescribed_IsNotWritten()
    {
        var source = Source("a.txt", "one");
        var version = Version(source);
        File.WriteAllText(source, "two");

        Assert.Throws<IOException>(() => Write(version, source));

        Assert.False(Files().Exists(version.Id));
        Assert.Empty(Directory.GetFiles(ContentFolder, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void AFileWrittenTwice_IsRefused()
    {
        var source = Source("a.txt", "one");
        var version = Version(source);
        Write(version, source);

        Assert.ThrowsAny<IOException>(() => Write(version, source));
    }

    [Fact]
    public void AFileNotThere_DoesNotExist_AndIsNotExported()
    {
        var target = Source("keep.txt", "kept");

        Assert.False(Files().Exists(Guid.NewGuid()));
        Assert.ThrowsAny<IOException>(() => Files().OpenRead(Guid.NewGuid()));
        Assert.ThrowsAny<IOException>(() => Files().Export(Guid.NewGuid(), target));
        Assert.Equal("kept", File.ReadAllText(target));
    }

    [Fact]
    public void ADamagedFile_LeavesTheTargetAsItWas()
    {
        var source = Source("a.txt", "content");
        var version = Version(source);
        Write(version, source);
        var sealedFile = Directory.GetFiles(ContentFolder, "*", SearchOption.AllDirectories).Single();
        var bytes = File.ReadAllBytes(sealedFile);
        bytes[^1] ^= 1;
        File.WriteAllBytes(sealedFile, bytes);
        var target = Source("keep.txt", "kept");

        Assert.Throws<InvalidDataException>(() => Files().Export(version.Id, target));

        Assert.Equal("kept", File.ReadAllText(target));
        Assert.False(File.Exists(target + ".tmp"));
    }

    [Fact]
    public void AFileUnderAnotherId_DoesNotOpen()
    {
        var source = Source("a.txt", "content");
        var version = Version(source);
        Write(version, source);
        var other = Guid.NewGuid();
        var otherPath = Path.Combine(ContentFolder, other.ToString("N")[..2], other.ToString("N") + VaultFolder.DatasetExtension);
        Directory.CreateDirectory(Path.GetDirectoryName(otherPath)!);
        File.Copy(Directory.GetFiles(ContentFolder, "*", SearchOption.AllDirectories).Single(), otherPath);

        using var opened = Files().OpenRead(other);
        Assert.Throws<InvalidDataException>(() => opened.CopyTo(Stream.Null));
    }

    [Fact]
    public void DescribingAndWriting_TellTheBytesReadSoFar_OncePerMegabyte_AndAtTheEnd()
    {
        var path = _temp.Sub("big.bin");
        File.WriteAllBytes(path, new byte[(3 * 1024 * 1024) + 10]);
        var described = new Reports();

        var file = DatasetFiles.Describe(path, described);
        var version = new DatasetEdited(Guid.NewGuid(), At, Guid.NewGuid(), file, []);
        var written = new Reports();
        using (var content = File.OpenRead(path))
        {
            Files().Write(version, content, written);
        }

        foreach (var reports in new[] { described.Values, written.Values })
        {
            Assert.Equal(file.Size, reports[^1]);
            Assert.InRange(reports.Count, 3, 5);
            Assert.Equal(reports.Order(), reports);
            Assert.Equal(reports.Distinct(), reports);
        }
    }

    [Fact]
    public void AnEmptyFile_TellsItsEnd_Once()
    {
        var reports = new Reports();

        DatasetFiles.Describe(Source("empty.txt", string.Empty), reports);

        Assert.Equal([0L], reports.Values);
    }

    public void Dispose() => _temp.Dispose();

    /// <summary>Keeps what it is told, at once (Progress&lt;T&gt; would post it later).</summary>
    private sealed class Reports : IProgress<long>
    {
        public List<long> Values { get; } = [];

        public void Report(long value) => Values.Add(value);
    }

    private DatasetFiles Files() => new(Folder, new KeyedBox(KeyRing.Of(_key)));

    private string Source(string name, string content)
    {
        var path = _temp.Sub(name);
        File.WriteAllText(path, content);
        return path;
    }

    private static DatasetEdited Version(string source) => new(Guid.NewGuid(), At, Guid.NewGuid(), DatasetFiles.Describe(source), []);

    private void Write(DatasetEdited version, string source)
    {
        using var content = File.OpenRead(source);
        Files().Write(version, content);
    }
}
