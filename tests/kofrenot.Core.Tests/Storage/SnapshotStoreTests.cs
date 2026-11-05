using System.Security.Cryptography;
using System.Text;

namespace Kofrenot.Core.Tests.Storage;

public sealed class SnapshotStoreTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly TempFolder _temp = new();
    private readonly RingLog _log = new();
    private readonly KeyedBox _box = new(KeyRing.Of(RandomNumberGenerator.GetBytes(SecretBox.KeySize)));
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
            new VaultNamed(Guid.NewGuid(), Start.AddMinutes(3), "Personal", []),
            new NodeRetyped(Guid.NewGuid(), Start.AddMinutes(4), dune.Id, "note", [dune.Id]),
        ];

        Store().Save(changes, highestFormat: 3, oldestWriter: 3);
        var snapshot = Store().Load();

        Assert.NotNull(snapshot);
        Assert.Equal(3, snapshot.HighestFormat);
        Assert.Equal(3, snapshot.OldestWriter);
        var loaded = snapshot.Changes;
        Assert.Equal(changes.Select(change => (change.Id, change.GetType())), loaded.Select(change => (change.Id, change.GetType())));
        Assert.Equal(dune, Assert.IsType<NodeAdded>(loaded[0]).Node);
        Assert.Equal(alias, loaded[4]);
        Assert.Equal([alias.Id], Assert.IsType<ChangesRemoved>(loaded[5]).Removes);
        Assert.Equal("Personal", Assert.IsType<VaultNamed>(loaded[6]).Name);
        Assert.Equal("note", Assert.IsType<NodeRetyped>(loaded[7]).Type);
    }

    [Fact]
    public void TheFile_IsEncrypted_WithNoTemporaryFileLeft()
    {
        Store().Save([new NodeAdded(Guid.NewGuid(), Start, new Node(Guid.NewGuid(), "generic", "Dune", Start))], VaultFormat.Current, VaultFormat.Current);

        Assert.DoesNotContain("Dune", Encoding.UTF8.GetString(File.ReadAllBytes(FilePath)));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(FilePath)!));
    }

    [Fact]
    public void ASnapshotOfAnotherVault_IsNotUsed()
    {
        Store().Save([], VaultFormat.Current, VaultFormat.Current);

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
    [InlineData("{\"format\":3,\"changes\":[]}")]
    [InlineData("{\"format\":4,\"changes\":[]}")]
    [InlineData("{\"format\":4,\"highestFormat\":4,\"changes\":[]}")]
    [InlineData("{\"format\":5,\"highestFormat\":4,\"changes\":[]}")]
    [InlineData("{\"format\":5,\"highestFormat\":4,\"oldestWriter\":4,\"changes\":[]}")]
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
        WriteSnapshot($"{{\"format\":5,\"highestFormat\":4,\"oldestWriter\":4,\"savedBy\":{VaultFormat.Current},\"changes\":[{{\"id\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\",\"change\":{{\"format\":99,\"op\":\"addNode\",\"at\":\"2026-10-16T09:12:00Z\"}}}}]}}");

        Assert.Null(Store().Load());
        Assert.Contains(_log.Entries, entry => entry.Message.Contains("newer format"));
    }

    [Fact]
    public void ASnapshotOfANewerKofrenot_IsNotUsed()
    {
        WriteSnapshot($"{{\"format\":5,\"highestFormat\":{VaultFormat.Current + 1},\"oldestWriter\":1,\"savedBy\":{VaultFormat.Current + 1},\"changes\":[]}}");

        Assert.Null(Store().Load());
        Assert.Contains(_log.Entries, entry => entry.Message.Contains($"saved by a kofrenot of format {VaultFormat.Current + 1}"));
    }

    [Fact]
    public void ASnapshotOfThisKofrenot_HoldingChangesOfANewerFormat_IsUsed()
    {
        WriteSnapshot($"{{\"format\":5,\"highestFormat\":{VaultFormat.Current + 1},\"oldestWriter\":{VaultFormat.Current + 1},\"savedBy\":{VaultFormat.Current},\"changes\":[]}}");

        var snapshot = Store().Load();

        Assert.NotNull(snapshot);
        Assert.Equal((VaultFormat.Current + 1, VaultFormat.Current + 1), (snapshot.HighestFormat, snapshot.OldestWriter));
    }

    // An older kofrenot skipped what it did not know of them: this one reads them again, whole.
    [Fact]
    public void ASnapshotOfAnOlderKofrenot_HoldingChangesNewerThanIt_IsNotUsed()
    {
        WriteSnapshot($"{{\"format\":5,\"highestFormat\":{VaultFormat.Current},\"oldestWriter\":1,\"savedBy\":{VaultFormat.Current - 1},\"changes\":[]}}");

        Assert.Null(Store().Load());
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Info && entry.Message.Contains("every change is read again"));
    }

    [Fact]
    public void ASnapshotOfAnOlderKofrenot_HoldingOnlyChangesItKnew_IsUsed()
    {
        WriteSnapshot($"{{\"format\":5,\"highestFormat\":{VaultFormat.Current - 1},\"oldestWriter\":1,\"savedBy\":{VaultFormat.Current - 1},\"changes\":[]}}");

        Assert.NotNull(Store().Load());
    }

    [Fact]
    public void AFieldOfAnUnknownKind_IsKeptInTheSnapshot()
    {
        var node = Guid.NewGuid();
        Change[] changes =
        [
            new NodeTypeDefined(Guid.NewGuid(), Start, "book", "Book", null, null, [new FieldDefinition("stars", "Stars", FieldKind.Unknown)], []),
            new FieldSet(Guid.NewGuid(), Start, node, "own", "stars", "Stars", FieldKind.Unknown, "4/5", []),
        ];

        Store().Save(changes, VaultFormat.Current + 1, VaultFormat.Current + 1);
        var loaded = Store().Load()!.Changes;

        Assert.Equal(FieldKind.Unknown, Assert.Single(Assert.IsType<NodeTypeDefined>(loaded[0]).Fields).Kind);
        Assert.Equal((FieldKind.Unknown, "4/5"), (Assert.IsType<FieldSet>(loaded[1]).Kind, ((FieldSet)loaded[1]).Value));
    }

    [Fact]
    public void ASaveThatFails_IsOnlyLogged()
    {
        // A folder where the file should be: it cannot be written.
        Directory.CreateDirectory(FilePath);

        Store().Save([], VaultFormat.Current, VaultFormat.Current);

        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("cannot be saved"));
    }

    public void Dispose() => _temp.Dispose();

    private SnapshotStore Store() => new(FilePath, _box, _vault, _log);

    private void WriteSnapshot(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllBytes(FilePath, _box.Seal(SealKind.Snapshot, Encoding.UTF8.GetBytes(json), _vault.ToByteArray()));
    }
}
