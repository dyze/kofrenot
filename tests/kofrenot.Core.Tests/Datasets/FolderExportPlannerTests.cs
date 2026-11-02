namespace Kofrenot.Core.Tests.Datasets;

public sealed class FolderExportPlannerTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly TempFolder _temp = new();
    private readonly KnowledgeGraph _graph = new();
    private readonly HashSet<Guid> _missing = [];

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void ADataset_IsItsLatestFile_AndANodeWithChildren_AFolderOfThem()
    {
        var trip = AddNode("Trip", TypeCatalog.Generic.Id);
        var days = Under(trip, AddNode("Days", TypeCatalog.Generic.Id));
        Under(days, Dataset("monday", "monday.md"));
        Under(trip, Dataset("map", "old map.pdf", "map.pdf"));
        var report = Dataset("report", "report.pdf");

        var export = Plan([trip, report]);

        Assert.Equal(["Trip", "report.pdf"], export.Top);
        Assert.Equal(["Trip", Path.Combine("Trip", "Days")], export.Folders);
        Assert.Equal([Path.Combine("Trip", "Days", "monday.md"), Path.Combine("Trip", "map.pdf"), "report.pdf"], export.Files.Select(file => file.Path));
        Assert.Equal((0, 0, false), (export.NotDownloaded, export.NotFiles, export.IsEmpty));
    }

    [Fact]
    public void SameNamesInAFolder_AreNumbered_AndUnsafeCharactersReplaced()
    {
        var export = Plan([Dataset("a", "a.txt"), Dataset("A", "A.TXT"), Dataset("b", "a.txt"), Dataset("q", "what?.txt"), AddUnder("x:y", Dataset("z", "z.txt"))]);

        Assert.Equal(["a.txt", "A (2).TXT", "a (3).txt", "what_.txt", "x_y"], export.Top);
    }

    [Fact]
    public void NotesAndFilesNotDownloaded_AreLeftOut_AndCounted()
    {
        var missing = Dataset("far", "far.pdf");
        _missing.Add(_graph.DatasetVersionsOf(missing.Id)[^1].Id);

        var export = Plan([AddNode("Ideas", TypeCatalog.Note.Id), AddNode("empty", TypeCatalog.Dataset.Id), missing]);

        Assert.True(export.IsEmpty);
        Assert.Equal((1, 2), (export.NotDownloaded, export.NotFiles));
    }

    [Fact]
    public void ANodeMetTwice_IsWrittenOnce_EvenInACycle()
    {
        var a = AddNode("A", TypeCatalog.Generic.Id);
        var b = Under(a, AddNode("B", TypeCatalog.Generic.Id));
        Under(b, a);
        var file = Under(b, Dataset("f", "f.txt"));

        var export = Plan([a, file, b]);

        Assert.Equal(["A"], export.Top);
        Assert.Equal([Path.Combine("A", "B", "f.txt")], export.Files.Select(exported => exported.Path));
    }

    [Fact]
    public void ADatasetWithChildren_IsItsFile_AndAFolder()
    {
        var album = Dataset("album", "album.zip");
        Under(album, Dataset("cover", "cover.png"));

        Assert.Equal(["album.zip", "album"], Plan([album]).Top);
    }

    [Fact]
    public void InAList_NodesHaveNoChildren()
    {
        var trip = AddNode("Trip", TypeCatalog.Generic.Id);
        Under(trip, Dataset("map", "map.pdf"));

        var export = new FolderExportPlanner(_graph, linkType: null).Plan([trip], _ => true);

        Assert.True(export.IsEmpty);
        Assert.Equal(1, export.NotFiles);
    }

    [Fact]
    public void WriteTo_CreatesTheFolders_ThenExportsEachFile()
    {
        var trip = AddNode("Trip", TypeCatalog.Generic.Id);
        Under(trip, AddNode("Empty", TypeCatalog.Generic.Id));
        Under(Under(trip, AddNode("Days", TypeCatalog.Generic.Id)), Dataset("monday", "monday.md"));
        var export = Plan([trip]);
        var exported = new List<Guid>();

        var top = export.WriteTo(_temp.Path, (version, path) =>
        {
            exported.Add(version);
            File.WriteAllText(path, "x");
        });

        Assert.Equal([_temp.Sub("Trip")], top);
        Assert.True(File.Exists(Path.Combine(_temp.Path, "Trip", "Days", "monday.md")));
        Assert.Equal([export.Files[0].Version.Id], exported);
        Assert.Equal(1, export.NotFiles);
    }

    private FolderExport Plan(IEnumerable<Node> nodes) =>
        new FolderExportPlanner(_graph, TypeCatalog.Contains.Id).Plan(nodes, version => !_missing.Contains(version));

    private Node AddNode(string name, string type)
    {
        var node = new Node(Guid.NewGuid(), type, name, Start);
        _graph.Add(node);
        return node;
    }

    /// <param name="files">Its versions' files, the last one the latest.</param>
    private Node Dataset(string name, params string[] files)
    {
        var node = AddNode(name, TypeCatalog.Dataset.Id);
        var replaced = new List<Guid>();
        foreach (var file in files)
        {
            var version = new DatasetEdited(Guid.NewGuid(), Start, node.Id, new DatasetFile(file, 1, new string('0', 64)), [.. replaced]);
            _graph.Apply(version);
            replaced.Add(version.Id);
        }

        return node;
    }

    private Node Under(Node parent, Node child)
    {
        _graph.Add(new Link(Guid.NewGuid(), TypeCatalog.Contains.Id, parent.Id, child.Id, Start));
        return child;
    }

    private Node AddUnder(string parentName, Node child)
    {
        Under(AddNode(parentName, TypeCatalog.Generic.Id), child);
        return _graph.FindNode(_graph.LinksOf(child.Id)[0].From)!;
    }
}
