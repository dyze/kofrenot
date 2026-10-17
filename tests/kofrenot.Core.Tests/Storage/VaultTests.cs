namespace Kofrenot.Core.Tests.Storage;

public sealed class VaultTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly TempFolder _temp = new();
    private readonly RingLog _log = new();
    private readonly VaultHeader _header;
    private readonly byte[] _key;

    public VaultTests() => (_header, _key) = VaultFolder.Create(Folder, "secret", iterations: 10);

    private string Folder => _temp.Sub("db");

    private string Cache => _temp.Sub("cache");

    [Fact]
    public void ANewVault_IsEmpty()
    {
        var vault = Open();

        Assert.Equal(_header.Id, vault.Id);
        Assert.Empty(vault.Graph.Nodes);
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
        var vault = Open();
        var change = new NodeAdded(Guid.NewGuid(), Start, NewNode("Dune"));

        vault.Append(change);

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
    public void AChangeOfANewerFormat_StopsTheVault_OnOpeningAndOnRefresh()
    {
        var reader = Open(snapshot: false);
        WriteChange("{\"format\":99,\"op\":\"rename\",\"at\":\"2026-10-16T09:12:00Z\"}");

        Assert.Equal(99, Assert.Throws<NewerVaultFormatException>(() => reader.Refresh()).Format);
        Assert.Throws<NewerVaultFormatException>(() => Open(snapshot: false));
    }

    [Fact]
    public void OtherFiles_InTheChangesFolder_AreIgnored()
    {
        var sub = Path.Combine(ChangesFolder, "3f");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "3f2504e04f8911d39a0c0305e82c3301.kfc.tmp"), "being written");
        File.WriteAllText(Path.Combine(sub, "not-an-id.kfc"), "?");
        File.WriteAllText(Path.Combine(sub, "desktop.ini"), "?");

        var vault = Open(snapshot: false);

        Assert.Empty(vault.Graph.Nodes);
        Assert.DoesNotContain(_log.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public void AMissingChangesFolder_MeansNoChanges_AndIsCreatedOnFirstAppend()
    {
        Directory.Delete(ChangesFolder);
        var vault = Open(snapshot: false);

        Assert.Empty(vault.Graph.Nodes);

        AddNode(vault, "Dune");
        Assert.Single(Directory.GetFiles(ChangesFolder, "*.kfc", SearchOption.AllDirectories));
    }

    [Fact]
    public void AChangeAppendedTwice_IsRefused()
    {
        var vault = Open(snapshot: false);
        var change = new NodeAdded(Guid.NewGuid(), Start, NewNode("Dune"));
        vault.Append(change);

        Assert.Throws<IOException>(() => vault.Append(change));
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
    public void ARename_IsFoundAgain_FromTheFiles_AndFromTheSnapshot()
    {
        var vault = Open();
        var dune = AddNode(vault, "Dune");

        vault.Append(new NodeRenamed(Guid.NewGuid(), Start.AddMinutes(1), dune.Id, "Dune (novel)", [dune.Id]));

        Assert.Equal("Dune (novel)", Open(snapshot: false).Graph.FindNode(dune.Id)!.Name);
        Directory.Delete(ChangesFolder, recursive: true);
        Assert.Equal("Dune (novel)", Open().Graph.FindNode(dune.Id)!.Name);
    }

    [Fact]
    public void AliasesAndRemovals_AreFoundAgain_FromTheFiles_AndFromTheSnapshot()
    {
        var vault = Open();
        var kept = new AliasAdded(Guid.NewGuid(), Start, "contains", IsReverse: true, "is in");
        var removed = new AliasAdded(Guid.NewGuid(), Start, "relates-to", IsReverse: false, "-");
        vault.Append(kept);
        vault.Append(removed);
        vault.Append(new ChangesRemoved(Guid.NewGuid(), Start.AddMinutes(1), [removed.Id]));

        Assert.Equal([kept.Id], Open(snapshot: false).Graph.Aliases.All.Select(alias => alias.Id));
        Directory.Delete(ChangesFolder, recursive: true);
        Assert.Equal([kept.Id], Open().Graph.Aliases.All.Select(alias => alias.Id));
    }

    [Fact]
    public void TheSnapshot_IsNamedByTheVaultId() =>
        Assert.Equal(Path.Combine(Cache, _header.Id.ToString("N") + ".kfs"), Vault.SnapshotOf(Cache, _header.Id));

    public void Dispose() => _temp.Dispose();

    private string ChangesFolder => Path.Combine(Folder, VaultFolder.ChangesFolder);

    private Vault Open(bool snapshot = true) =>
        Vault.Open(Folder, _header, _key, snapshot ? Cache : null, _log);

    private static Node AddNode(Vault vault, string name)
    {
        var node = NewNode(name);
        vault.Append(new NodeAdded(Guid.NewGuid(), node.Created, node));
        return node;
    }

    // A change file as another kofrenot would write it.
    private void WriteChange(string json)
    {
        var id = Guid.NewGuid();
        var name = id.ToString("N");
        var path = Path.Combine(ChangesFolder, name[..2], name + ".kfc");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new SecretBox(_key).Seal("KFC1"u8, System.Text.Encoding.UTF8.GetBytes(json), id.ToByteArray()));
    }

    private static Node NewNode(string name) => new(Guid.NewGuid(), TypeCatalog.Generic.Id, name, Start);

    private static Link NewLink(Node from, Node to) => new(Guid.NewGuid(), TypeCatalog.RelatesTo.Id, from.Id, to.Id, Start);
}
