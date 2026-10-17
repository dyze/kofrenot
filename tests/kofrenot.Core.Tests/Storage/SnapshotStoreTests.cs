using System.Security.Cryptography;
using System.Text;

namespace Kofrenot.Core.Tests.Storage;

public sealed class SnapshotStoreTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly TempFolder _temp = new();
    private readonly RingLog _log = new();
    private readonly SecretBox _box = new(RandomNumberGenerator.GetBytes(SecretBox.KeySize));
    private readonly Guid _vault = Guid.NewGuid();

    private string FilePath => Path.Combine(_temp.Sub("cache"), "db.kfs");

    [Fact]
    public void NoFile_MeansNoSnapshot() =>
        Assert.Null(Store().Load());

    [Fact]
    public void SavedChanges_AreLoaded_OfEveryKind()
    {
        var dune = new Node(Guid.NewGuid(), "generic", "Dune", Start);
        var herbert = new Node(Guid.NewGuid(), "generic", "Frank Herbert", Start);
        var alias = new AliasAdded(Guid.NewGuid(), Start, "contains", IsReverse: true, "is in");
        Change[] changes =
        [
            new NodeAdded(Guid.NewGuid(), Start, dune),
            new NodeAdded(Guid.NewGuid(), Start, herbert),
            new LinkAdded(Guid.NewGuid(), Start, new Link(Guid.NewGuid(), "relates-to", dune.Id, herbert.Id, Start)),
            new NodeRenamed(Guid.NewGuid(), Start.AddMinutes(1), dune.Id, "Dune (novel)", [dune.Id]),
            alias,
            new ChangesRemoved(Guid.NewGuid(), Start.AddMinutes(2), [alias.Id]),
        ];

        Store().Save(changes);
        var loaded = Store().Load();

        Assert.NotNull(loaded);
        Assert.Equal(changes.Select(change => (change.Id, change.GetType())), loaded.Select(change => (change.Id, change.GetType())));
        Assert.Equal(dune, Assert.IsType<NodeAdded>(loaded[0]).Node);
        Assert.Equal(alias, loaded[4]);
        Assert.Equal([alias.Id], Assert.IsType<ChangesRemoved>(loaded[5]).Removes);
    }

    [Fact]
    public void TheFile_IsEncrypted_WithNoTemporaryFileLeft()
    {
        Store().Save([new NodeAdded(Guid.NewGuid(), Start, new Node(Guid.NewGuid(), "generic", "Dune", Start))]);

        Assert.DoesNotContain("Dune", Encoding.UTF8.GetString(File.ReadAllBytes(FilePath)));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(FilePath)!));
    }

    [Fact]
    public void ASnapshotOfAnotherVault_IsNotUsed()
    {
        Store().Save([]);

        Assert.Null(new SnapshotStore(FilePath, _box, Guid.NewGuid(), _log).Load());
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public void ADamagedSnapshot_IsNotUsed()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllBytes(FilePath, [1, 2, 3]);

        Assert.Null(Store().Load());
    }

    [Theory]
    [InlineData("{\"format\":2,\"changes\":[],\"nodes\":[],\"links\":[],\"renames\":[]}")]
    [InlineData("{\"format\":3}")]
    [InlineData("{\"format\":3,\"changes\":[{\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\"}]}")]
    [InlineData("{\"format\":3,\"changes\":[{\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"change\":{\"format\":2,\"op\":\"unknown\",\"at\":\"2026-10-16T09:12:00Z\"}}]}")]
    [InlineData("null")]
    [InlineData("{ not json")]
    public void ASnapshotOfAnotherFormat_IsNotUsed(string json)
    {
        WriteSnapshot(json);

        Assert.Null(Store().Load());
    }

    [Fact]
    public void ASnapshotOfKofrenot01_IsNotUsed_WithoutAWarning()
    {
        WriteSnapshot("{\"format\":2,\"changes\":[\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\"],\"nodes\":[],\"links\":[],\"renames\":[]}");

        Assert.Null(Store().Load());
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Info && entry.Message.Contains("another format"));
        Assert.DoesNotContain(_log.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public void ASnapshotHoldingANewerFormat_IsNotUsed()
    {
        WriteSnapshot("{\"format\":3,\"changes\":[{\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"change\":{\"format\":99,\"op\":\"addNode\",\"at\":\"2026-10-16T09:12:00Z\"}}]}");

        Assert.Null(Store().Load());
        Assert.Contains(_log.Entries, entry => entry.Message.Contains("newer format"));
    }

    [Fact]
    public void ASaveThatFails_IsOnlyLogged()
    {
        // A folder where the file should be: it cannot be written.
        Directory.CreateDirectory(FilePath);

        Store().Save([]);

        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("cannot be saved"));
    }

    public void Dispose() => _temp.Dispose();

    private SnapshotStore Store() => new(FilePath, _box, _vault, _log);

    private void WriteSnapshot(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllBytes(FilePath, _box.Seal("KFS1"u8, Encoding.UTF8.GetBytes(json), _vault.ToByteArray()));
    }
}
