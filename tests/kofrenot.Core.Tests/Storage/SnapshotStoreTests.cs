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
    public void ASavedGraph_IsLoaded()
    {
        var graph = new KnowledgeGraph();
        var dune = new Node(Guid.NewGuid(), "generic", "Dune", Start);
        var herbert = new Node(Guid.NewGuid(), "generic", "Frank Herbert", Start);
        var link = new Link(Guid.NewGuid(), "relates-to", dune.Id, herbert.Id, Start);
        graph.Add(dune);
        graph.Add(herbert);
        graph.Add(link);
        var changes = new[] { Guid.NewGuid(), Guid.NewGuid() };

        Store().Save(graph, changes);
        var snapshot = Store().Load();

        Assert.NotNull(snapshot);
        Assert.Equal(changes, snapshot.Changes);
        Assert.Equal([dune, herbert], snapshot.Nodes.OrderBy(node => node.Name));
        Assert.Equal([link], snapshot.Links);
        Assert.Empty(snapshot.Renames);
    }

    [Fact]
    public void Renames_AreKept_WithTheNodesAsAdded()
    {
        var graph = new KnowledgeGraph();
        var dune = new Node(Guid.NewGuid(), "generic", "Dune", Start);
        graph.Add(dune);
        var rename = new NodeRenamed(Guid.NewGuid(), Start.AddMinutes(1), dune.Id, "Dune (novel)", [dune.Id]);
        graph.Rename(rename);

        Store().Save(graph, []);
        var snapshot = Store().Load()!;

        Assert.Equal([dune], snapshot.Nodes);
        var kept = Assert.Single(snapshot.Renames);
        Assert.Equal((rename.Id, rename.Node, rename.Name), (kept.Id, kept.Node, kept.Name));
        Assert.Equal([dune.Id], kept.Replaces);
    }

    [Fact]
    public void TheFile_IsEncrypted_WithNoTemporaryFileLeft()
    {
        var graph = new KnowledgeGraph();
        graph.Add(new Node(Guid.NewGuid(), "generic", "Dune", Start));

        Store().Save(graph, []);

        Assert.DoesNotContain("Dune", Encoding.UTF8.GetString(File.ReadAllBytes(FilePath)));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(FilePath)!));
    }

    [Fact]
    public void ASnapshotOfAnotherVault_IsNotUsed()
    {
        Store().Save(new KnowledgeGraph(), []);

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
    [InlineData("{\"format\":1,\"changes\":[],\"nodes\":[],\"links\":[],\"renames\":[]}")]
    [InlineData("{\"format\":2,\"changes\":[],\"nodes\":[],\"links\":[]}")]
    [InlineData("{\"format\":1,\"nodes\":[],\"links\":[]}")]
    [InlineData("{\"format\":1,\"changes\":[],\"links\":[]}")]
    [InlineData("{\"format\":1,\"changes\":[],\"nodes\":[]}")]
    [InlineData("null")]
    [InlineData("{ not json")]
    public void ASnapshotOfAnotherFormat_IsNotUsed(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllBytes(FilePath, _box.Seal("KFS1"u8, Encoding.UTF8.GetBytes(json), _vault.ToByteArray()));

        Assert.Null(Store().Load());
    }

    [Fact]
    public void ASaveThatFails_IsOnlyLogged()
    {
        // A folder where the file should be: it cannot be written.
        Directory.CreateDirectory(FilePath);

        Store().Save(new KnowledgeGraph(), []);

        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("cannot be saved"));
    }

    public void Dispose() => _temp.Dispose();

    private SnapshotStore Store() => new(FilePath, _box, _vault, _log);
}
