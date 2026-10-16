namespace Kofrenot.Core.Tests.Storage;

public sealed class DatabaseTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly TempFolder _temp = new();
    private readonly RingLog _log = new();
    private readonly DatabaseHeader _header;
    private readonly byte[] _key;

    public DatabaseTests() => (_header, _key) = DatabaseFolder.Create(Folder, "secret", iterations: 10);

    private string Folder => _temp.Sub("db");

    private string Cache => _temp.Sub("cache");

    [Fact]
    public void ANewDatabase_IsEmpty()
    {
        var database = Open();

        Assert.Equal(_header.Id, database.Id);
        Assert.Empty(database.Graph.Nodes);
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Info && entry.Message.Contains("opened"));
    }

    [Fact]
    public void AnAppendedChange_IsApplied_AndFoundAgainOnReopening()
    {
        var dune = AddNode(Open(snapshot: false), "Dune");

        var reopened = Open(snapshot: false);

        Assert.Equal(dune, reopened.Graph.FindNode(dune.Id));
    }

    [Fact]
    public void EachChange_IsOneFile_InASubFolder_WithNoTemporaryFileLeft()
    {
        var database = Open();
        var change = new NodeAdded(Guid.NewGuid(), Start, NewNode("Dune"));

        database.Append(change);

        var name = change.Id.ToString("N");
        var file = Assert.Single(Directory.GetFiles(ChangesFolder, "*", SearchOption.AllDirectories));
        Assert.Equal(Path.Combine(ChangesFolder, name[..2], name + ".kfc"), file);
    }

    [Fact]
    public void ChangesOfAnotherApp_AreSeenAtTheNextRefresh()
    {
        var mine = Open(snapshot: false);
        var other = Open(snapshot: false);

        var (dune, herbert) = (AddNode(other, "Dune"), AddNode(other, "Frank Herbert"));
        other.Append(new LinkAdded(Guid.NewGuid(), Start, NewLink(dune, herbert)));

        Assert.Equal(3, mine.Refresh());
        Assert.Single(mine.Graph.LinksOf(dune.Id));
        Assert.Equal(0, mine.Refresh());
    }

    [Fact]
    public void TwoAppsAddingAtOnce_BothKeepTheirChanges()
    {
        var first = Open(snapshot: false);
        var second = Open(snapshot: false);

        AddNode(first, "Dune");
        AddNode(second, "dune");
        first.Refresh();
        second.Refresh();

        Assert.Equal(2, first.Graph.NodesNamed("Dune").Count);
        Assert.Equal(2, second.Graph.NodesNamed("Dune").Count);
    }

    [Fact]
    public void ALinkWrittenBeforeItsNodes_IsShown()
    {
        var writer = Open(snapshot: false);
        var dune = NewNode("Dune");
        var herbert = NewNode("Frank Herbert");
        writer.Append(new LinkAdded(Guid.NewGuid(), Start.AddMinutes(2), NewLink(dune, herbert)));
        writer.Append(new NodeAdded(Guid.NewGuid(), Start, dune));
        writer.Append(new NodeAdded(Guid.NewGuid(), Start.AddMinutes(1), herbert));

        var reader = Open(snapshot: false);

        Assert.Single(reader.Graph.LinksOf(dune.Id));
    }

    [Fact]
    public void AnUnreadableFile_IsSkipped_ReportedOnce_AndReadOnceRepaired()
    {
        var writer = Open(snapshot: false);
        var dune = AddNode(writer, "Dune");
        var file = Directory.GetFiles(ChangesFolder, "*.kfc", SearchOption.AllDirectories)[0];
        var content = File.ReadAllBytes(file);
        File.WriteAllBytes(file, [1, 2, 3]);

        var reader = Open(snapshot: false);
        reader.Refresh();

        Assert.Empty(reader.Graph.Nodes);
        Assert.Single(_log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("skipped"));

        File.WriteAllBytes(file, content);
        Assert.Equal(1, reader.Refresh());
        Assert.NotNull(reader.Graph.FindNode(dune.Id));
    }

    [Fact]
    public void OtherFiles_InTheChangesFolder_AreIgnored()
    {
        var sub = Path.Combine(ChangesFolder, "3f");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "3f2504e04f8911d39a0c0305e82c3301.kfc.tmp"), "being written");
        File.WriteAllText(Path.Combine(sub, "not-an-id.kfc"), "?");
        File.WriteAllText(Path.Combine(sub, "desktop.ini"), "?");

        var database = Open(snapshot: false);

        Assert.Empty(database.Graph.Nodes);
        Assert.DoesNotContain(_log.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public void AMissingChangesFolder_MeansNoChanges_AndIsCreatedOnFirstAppend()
    {
        Directory.Delete(ChangesFolder);
        var database = Open(snapshot: false);

        Assert.Empty(database.Graph.Nodes);

        AddNode(database, "Dune");
        Assert.Single(Directory.GetFiles(ChangesFolder, "*.kfc", SearchOption.AllDirectories));
    }

    [Fact]
    public void AChangeAppendedTwice_IsRefused()
    {
        var database = Open(snapshot: false);
        var change = new NodeAdded(Guid.NewGuid(), Start, NewNode("Dune"));
        database.Append(change);

        Assert.Throws<IOException>(() => database.Append(change));
    }

    [Fact]
    public void TheSnapshot_SpareReadingTheChangesAgain()
    {
        var dune = AddNode(Open(), "Dune");
        // Gone from the folder: only the snapshot can still know it.
        Directory.Delete(ChangesFolder, recursive: true);

        var reopened = Open();

        Assert.Equal(dune, reopened.Graph.FindNode(dune.Id));
        Assert.Contains(_log.Entries, entry => entry.Message.Contains("1 changes from the snapshot, 0 read"));
    }

    [Fact]
    public void TheSnapshot_KeepsLinks_AndOnlyTheMissingChangesAreRead()
    {
        var first = Open();
        var (dune, herbert) = (AddNode(first, "Dune"), AddNode(first, "Frank Herbert"));
        first.Append(new LinkAdded(Guid.NewGuid(), Start, NewLink(dune, herbert)));
        AddNode(Open(snapshot: false), "Arrakis");

        var reopened = Open();

        Assert.Single(reopened.Graph.LinksOf(dune.Id));
        Assert.Equal(3, reopened.Graph.Nodes.Count);
        Assert.Contains(_log.Entries, entry => entry.Message.Contains("3 changes from the snapshot, 1 read"));
    }

    [Fact]
    public void ARefreshThatReadsChanges_UpdatesTheSnapshot()
    {
        var mine = Open();
        AddNode(Open(snapshot: false), "Dune");
        mine.Refresh();
        Directory.Delete(ChangesFolder, recursive: true);

        Assert.Single(Open().Graph.Nodes);
    }

    [Fact]
    public void TheSnapshot_IsNamedByTheDatabaseId() =>
        Assert.Equal(Path.Combine(Cache, _header.Id.ToString("N") + ".kfs"), Database.SnapshotOf(Cache, _header.Id));

    public void Dispose() => _temp.Dispose();

    private string ChangesFolder => Path.Combine(Folder, DatabaseFolder.ChangesFolder);

    private Database Open(bool snapshot = true) =>
        Database.Open(Folder, _header, _key, snapshot ? Cache : null, _log);

    private static Node AddNode(Database database, string name)
    {
        var node = NewNode(name);
        database.Append(new NodeAdded(Guid.NewGuid(), node.Created, node));
        return node;
    }

    private static Node NewNode(string name) => new(Guid.NewGuid(), TypeCatalog.Generic.Id, name, Start);

    private static Link NewLink(Node from, Node to) => new(Guid.NewGuid(), TypeCatalog.RelatesTo.Id, from.Id, to.Id, Start);
}
