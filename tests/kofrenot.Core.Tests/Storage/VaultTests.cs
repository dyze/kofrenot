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
    public void ANoteVersion_IsSaved_WithItsContent_AndFoundAgainOnReopening()
    {
        var vault = Open();
        var note = AddNode(vault, "Ideas");
        var version = new NoteEdited(Guid.NewGuid(), Start, note.Id, []);

        vault.Save(version, "# Ideas");

        var reopened = Open(snapshot: false);
        Assert.Equal([version.Id], reopened.Graph.NoteVersionsOf(note.Id).Select(saved => saved.Id));
        Assert.Equal("# Ideas", reopened.Notes.Read(version.Id));
        Assert.Equal([version.Id], Open().Graph.NoteVersionsOf(note.Id).Select(saved => saved.Id));
    }

    [Fact]
    public void ACopy_WritesTheContentOfItsNotesAndDatasets_UnderTheirNewVersions()
    {
        var vault = Open();
        var (projects, ideas, survey, archive) = (AddNode(vault, "Projects"), AddNode(vault, "Ideas"), AddNode(vault, "Survey"), AddNode(vault, "Archive"));
        vault.Append(new LinkAdded(Guid.NewGuid(), Start, new Link(Guid.NewGuid(), TypeCatalog.Contains.Id, projects.Id, ideas.Id, Start)));
        vault.Append(new LinkAdded(Guid.NewGuid(), Start, new Link(Guid.NewGuid(), TypeCatalog.Contains.Id, projects.Id, survey.Id, Start)));
        vault.Save(new NoteEdited(Guid.NewGuid(), Start, ideas.Id, []), "# Ideas");
        var source = _temp.Sub("survey.csv");
        File.WriteAllText(source, "a,b\n");
        vault.Save(new DatasetEdited(Guid.NewGuid(), Start, survey.Id, DatasetFiles.Describe(source), []), () => File.OpenRead(source));
        var plan = new CopyPlanner(vault.Graph, TypeCatalog.Contains.Id).Plan(projects, archive, _ => true);

        vault.WriteCopies(plan.Contents);
        plan.Changes.ToList().ForEach(vault.Append);

        var reopened = Open(snapshot: false);
        var note = Assert.IsType<NoteEdited>(plan.Contents.Single(copy => copy.From.Node == ideas.Id).To);
        Assert.Equal("# Ideas", reopened.Notes.Read(Assert.Single(reopened.Graph.NoteVersionsOf(note.Node)).Id));
        var dataset = Assert.IsType<DatasetEdited>(plan.Contents.Single(copy => copy.From.Node == survey.Id).To);
        var target = _temp.Sub("exported.csv");
        reopened.Datasets.Export(Assert.Single(reopened.Graph.DatasetVersionsOf(dataset.Node)).Id, target);
        Assert.Equal("a,b\n", File.ReadAllText(target));
    }

    [Fact]
    public void ACopy_OfANoteNotSynced_IsRefused()
    {
        var vault = Open();
        var copy = new ContentCopy(new NoteEdited(Guid.NewGuid(), Start, Guid.NewGuid(), []), new NoteEdited(Guid.NewGuid(), Start, Guid.NewGuid(), []));

        Assert.Throws<IOException>(() => vault.WriteCopies([copy]));
    }

    [Fact]
    public void ADatasetVersion_IsSaved_WithItsFile_AndFoundAgainOnReopening()
    {
        var vault = Open();
        var dataset = AddNode(vault, "Survey");
        var source = _temp.Sub("survey.csv");
        File.WriteAllText(source, "a,b\n1,2\n");
        var version = new DatasetEdited(Guid.NewGuid(), Start, dataset.Id, DatasetFiles.Describe(source), []);

        vault.Save(version, () => File.OpenRead(source));

        var reopened = Open(snapshot: false);
        Assert.Equal([version], reopened.Graph.DatasetVersionsOf(dataset.Id).Select(saved => saved with { Replaces = version.Replaces }));
        var target = _temp.Sub("exported.csv");
        reopened.Datasets.Export(version.Id, target);
        Assert.Equal("a,b\n1,2\n", File.ReadAllText(target));
        Assert.Equal([version.Id], Open().Graph.DatasetVersionsOf(dataset.Id).Select(saved => saved.Id));
    }

    [Fact]
    public void ADatasetVersionNotMatchingItsFile_IsNotSaved()
    {
        var vault = Open();
        var dataset = AddNode(vault, "Survey");
        var version = new DatasetEdited(Guid.NewGuid(), Start, dataset.Id, new DatasetFile("a.csv", 3, new string('0', 64)), []);

        Assert.Throws<IOException>(() => vault.Save(version, () => new MemoryStream([1, 2, 3])));

        Assert.Empty(vault.Graph.DatasetVersionsOf(dataset.Id));
        Assert.False(vault.Datasets.Exists(version.Id));
    }

    [Fact]
    public void AnImage_IsSavedWithTheThumbnailMade_ReadingItAgainForItsFile()
    {
        var vault = Open();
        var dataset = AddNode(vault, "Photo");
        byte[] image = [1, 2, 3];
        var version = ImageVersion(dataset, "photo.png", image);
        var maker = new FakeThumbnailMaker([9, 9]);

        vault.Save(version, () => new MemoryStream(image), maker);

        Assert.Equal([image], maker.Read);
        Assert.Equal([9, 9], Open().Thumbnails.Read(version.Id));
        Assert.True(vault.Datasets.Exists(version.Id));
    }

    [Theory]
    [InlineData("notes.txt", true)]
    [InlineData("photo.png", false)]
    public void NoThumbnail_IsSaved_ForAFileOtherThanAnImage_OrAnImageNotDecoded(string name, bool makesOne)
    {
        var vault = Open();
        var dataset = AddNode(vault, "File");
        byte[] content = [1, 2, 3];
        var version = ImageVersion(dataset, name, content);
        var maker = new FakeThumbnailMaker(makesOne ? [9] : null);

        vault.Save(version, () => new MemoryStream(content), maker);

        Assert.Null(vault.Thumbnails.Read(version.Id));
        Assert.Equal(name == "photo.png" ? 1 : 0, maker.Read.Count);
        Assert.True(vault.Datasets.Exists(version.Id));
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

    private static DatasetEdited ImageVersion(Node dataset, string name, byte[] content) =>
        new(Guid.NewGuid(), Start, dataset.Id, new DatasetFile(name, content.Length, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content))), []);

    /// <summary>Gives the same thumbnail for any image, or none; keeps what it read.</summary>
    private sealed class FakeThumbnailMaker(byte[]? thumbnail) : IThumbnailMaker
    {
        public List<byte[]> Read { get; } = [];

        public byte[]? Make(Stream image)
        {
            var read = new MemoryStream();
            image.CopyTo(read);
            Read.Add(read.ToArray());
            return thumbnail;
        }
    }

    private static Link NewLink(Node from, Node to) => new(Guid.NewGuid(), TypeCatalog.RelatesTo.Id, from.Id, to.Id, Start);
}
