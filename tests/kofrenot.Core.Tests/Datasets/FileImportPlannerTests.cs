namespace Kofrenot.Core.Tests.Datasets;

public sealed class FileImportPlannerTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly ManualClock _clock = new();
    private readonly KnowledgeGraph _graph = new();
    private readonly FileImportPlanner _planner;

    public FileImportPlannerTests()
    {
        _planner = new FileImportPlanner(_graph, _clock);
        Directory.CreateDirectory(_temp.Path);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void ThePreview_NamesEachDatasetAfterItsFile_AndCountsTheFilesAndTheirSize()
    {
        var preview = _planner.Preview([File("Invoice 2026.pdf", 100), File("holiday.jpg", 250)]);

        Assert.Equal(["Invoice 2026", "holiday"], preview.Rows.Select(row => row.NodeName));
        Assert.Equal(["pdf", "jpg"], preview.Rows.Select(row => row.Type.Extension));
        Assert.Equal(2, preview.Count);
        Assert.Equal(350, preview.TotalSize);
        Assert.Empty(preview.Skipped);
        Assert.Null(preview.Under);
    }

    [Fact]
    public void TheHeading_SaysHowManyFilesAndTheirSize()
    {
        Assert.Equal("Nothing to add", _planner.Preview(Array.Empty<ImportFile>()).Heading);
        Assert.Equal("Add 1 file · 100 bytes", _planner.Preview([File("a.txt", 100)]).Heading);
        Assert.Equal("Add 2 files · 300 bytes", _planner.Preview([File("a.txt", 100), File("b.txt", 200)]).Heading);
    }

    [Fact]
    public void FoldersAndMissingFiles_AreSkipped()
    {
        var folder = _temp.Sub("Photos");
        Directory.CreateDirectory(folder);

        var preview = _planner.Preview([File("a.txt", 1), folder + Path.DirectorySeparatorChar, _temp.Sub("gone.txt")]);

        Assert.Equal(["a"], preview.Rows.Select(row => row.NodeName));
        Assert.Equal(["Photos", "gone.txt"], preview.Skipped);
    }

    [Fact]
    public void ANameTaken_InTheVaultOrByAnotherFile_IsAHomonym()
    {
        _graph.Add(new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Notes", _clock.Now));

        var preview = _planner.Preview([File("notes.txt", 1), File("report.pdf", 1), File("Report.docx", 1)]);

        Assert.Equal([true, false, true], preview.Rows.Select(row => row.IsHomonym));
    }

    [Theory]
    [InlineData("report.pdf", "report")]
    [InlineData(".gitignore", ".gitignore")]
    [InlineData("archive.tar.gz", "archive.tar")]
    [InlineData("README", "README")]
    public void TheNodeName_IsTheFileNameWithoutItsExtension(string file, string name) =>
        Assert.Equal(name, FileImportPlanner.NodeNameOf(file));

    [Fact]
    public void ThePlan_AddsADatasetPerFile_LinkedUnderTheParentWhenGiven()
    {
        var parent = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Projects", _clock.Now);
        _graph.Add(parent);
        var preview = _planner.Preview([File("a.txt", 1), File("b.txt", 2)], new ParentLink(parent, TypeCatalog.Contains.Id));

        var plan = _planner.Plan(preview);

        var nodes = plan.Changes.OfType<NodeAdded>().Select(added => added.Node).ToList();
        Assert.Equal(["a", "b"], nodes.Select(node => node.Name));
        Assert.All(nodes, node => Assert.Equal((TypeCatalog.Dataset.Id, _clock.Now), (node.Type, node.Created)));
        Assert.Equal(nodes.Select(node => node.Id), plan.Changes.OfType<LinkAdded>().Select(added => added.Link.To));
        Assert.All(plan.Changes.OfType<LinkAdded>(), added => Assert.Equal((TypeCatalog.Contains.Id, parent.Id), (added.Link.Type, added.Link.From)));
        Assert.Equal(nodes, plan.Files.Select(file => file.Node));
        Assert.Equal(["a.txt", "b.txt"], plan.Files.Select(file => file.File.Name));
    }

    [Fact]
    public void WithoutAParent_ThePlanAddsNoLink()
    {
        var plan = _planner.Plan(_planner.Preview([File("a.txt", 1)]));

        Assert.Single(plan.Changes);
        Assert.IsType<NodeAdded>(plan.Changes[0]);
    }

    [Fact]
    public void AFileImported_FromAPath_IsReadAgainEachTimeItIsOpened()
    {
        var file = ImportFile.FromPath(File("a.txt", 3));

        using (var first = file.Open())
        {
            Assert.Equal(3, first.Length);
        }

        using var second = file.Open();
        Assert.Equal((3L, "a.txt"), (second.Length, file.Name));
    }

    private string File(string name, int size)
    {
        var path = _temp.Sub(name);
        System.IO.File.WriteAllBytes(path, new byte[size]);
        return path;
    }
}
