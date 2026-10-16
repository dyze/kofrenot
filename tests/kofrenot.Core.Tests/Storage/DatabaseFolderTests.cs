using System.Text;

namespace Kofrenot.Core.Tests.Storage;

public sealed class DatabaseFolderTests : IDisposable
{
    private const int Iterations = 10;

    private readonly TempFolder _temp = new();

    private string Folder => _temp.Sub("db");

    [Fact]
    public void AMissingFolder_IsEmpty() =>
        Assert.Equal(FolderKind.Empty, DatabaseFolder.Inspect(Folder));

    [Fact]
    public void AnEmptyFolder_IsEmpty()
    {
        Directory.CreateDirectory(Folder);

        Assert.Equal(FolderKind.Empty, DatabaseFolder.Inspect(Folder));
    }

    [Fact]
    public void AFolderWithOtherFiles_IsOther()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "notes.txt"), "mine");

        Assert.Equal(FolderKind.Other, DatabaseFolder.Inspect(Folder));
    }

    [Fact]
    public void ACreatedDatabase_IsADatabase_AndUnlocksWithItsPassPhrase()
    {
        var (header, masterKey) = DatabaseFolder.Create(Folder, "secret", Iterations);

        Assert.Equal(FolderKind.Database, DatabaseFolder.Inspect(Folder));
        Assert.True(Directory.Exists(Path.Combine(Folder, DatabaseFolder.ChangesFolder)));
        var read = DatabaseHeader.Read(DatabaseFolder.HeaderOf(Folder));
        Assert.Equal(header.Id, read.Id);
        Assert.Equal(DatabaseHeader.CurrentFormat, read.Format);
        Assert.Equal(masterKey, read.Unlock("secret"));
        Assert.Null(read.Unlock("wrong"));
    }

    [Fact]
    public void TheHeader_HoldsNoSecret()
    {
        DatabaseFolder.Create(Folder, "my secret phrase", Iterations);

        Assert.DoesNotContain("my secret phrase", File.ReadAllText(DatabaseFolder.HeaderOf(Folder)));
    }

    [Fact]
    public void ADatabase_IsNotCreatedInAFolderInUse()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "notes.txt"), "mine");

        Assert.Throws<IOException>(() => DatabaseFolder.Create(Folder, "secret", Iterations));
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

        Assert.Throws<InvalidDataException>(() => DatabaseHeader.Read(path));
    }

    [Fact]
    public void AHeaderOfANewerFormat_IsRefused()
    {
        var path = WriteHeader("{\"format\":2,\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\"}");

        Assert.Throws<NotSupportedException>(() => DatabaseHeader.Read(path));
    }

    public void Dispose() => _temp.Dispose();

    private string WriteHeader(string json)
    {
        Directory.CreateDirectory(Folder);
        var path = DatabaseFolder.HeaderOf(Folder);
        File.WriteAllText(path, json, Encoding.UTF8);
        return path;
    }
}
