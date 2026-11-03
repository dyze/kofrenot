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
        Assert.Equal(["pdf", "jpg"], preview.Rows.Select(row => row.Type!.Extension));
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
    public void TheHeading_SaysHowManyFolders_TooWhenThereAreSome()
    {
        Directory.CreateDirectory(_temp.Sub(Path.Combine("A", "B")));
        File(Path.Combine("C", "c.txt"), 5);

        Assert.Equal("Add 2 folders · 0 bytes", _planner.Preview([_temp.Sub("A")]).Heading);
        Assert.Equal("Add 1 file, 1 folder · 5 bytes", _planner.Preview([_temp.Sub("C")]).Heading);
    }

    [Fact]
    public void MissingFiles_AreSkipped()
    {
        var preview = _planner.Preview([File("a.txt", 1), _temp.Sub("gone.txt")]);

        Assert.Equal(["a"], preview.Rows.Select(row => row.NodeName));
        Assert.Equal(["gone.txt"], preview.Skipped);
    }

    [Fact]
    public void AFolder_BecomesANode_ItsFoldersThenItsFilesUnderIt_ByName()
    {
        File(Path.Combine("Trip", "b.txt"), 2);
        File(Path.Combine("Trip", "A.jpg"), 3);
        File(Path.Combine("Trip", "Days", "monday.md"), 4);
        Directory.CreateDirectory(_temp.Sub(Path.Combine("Trip", "Empty")));

        var preview = _planner.Preview([_temp.Sub("Trip") + Path.DirectorySeparatorChar, File("map.pdf", 1)]);

        Assert.Equal(["Trip", "Days", "monday", "Empty", "A", "b", "map"], preview.Rows.Select(row => row.NodeName));
        Assert.Equal([0, 1, 2, 1, 1, 1, 0], preview.Rows.Select(row => row.Depth));
        Assert.Equal([null, "Trip", "Days", "Trip", "Trip", "Trip", null], preview.Rows.Select(row => row.Folder?.NodeName));
        Assert.Equal([true, true, false, true, false, false, false], preview.Rows.Select(row => row.IsFolder));
        Assert.Equal(["Folder", "Folder", "md"], preview.Rows.Take(3).Select(row => row.Type?.Extension ?? row.TypeName));
        Assert.Equal((4, 3, 10L), (preview.FileCount, preview.FolderCount, preview.TotalSize));
        Assert.Equal(0, preview.Rows[0].Size);
        Assert.Empty(preview.Skipped);
    }

    [Fact]
    public void HiddenItems_InAFolder_AreLeftOut()
    {
        var hidden = File(Path.Combine("Trip", "desktop.ini"), 1);
        System.IO.File.SetAttributes(hidden, FileAttributes.Hidden | FileAttributes.System);
        File(Path.Combine("Trip", "a.txt"), 1);

        var preview = _planner.Preview([_temp.Sub("Trip")]);

        Assert.Equal(["Trip", "a"], preview.Rows.Select(row => row.NodeName));
        Assert.Empty(preview.Skipped);
    }

    [Fact]
    public void AFolderName_TakenInTheVault_IsAHomonym()
    {
        _graph.Add(new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "trip", _clock.Now));
        Directory.CreateDirectory(_temp.Sub("Trip"));

        Assert.True(Assert.Single(_planner.Preview([_temp.Sub("Trip")]).Rows).IsHomonym);
    }

    [Fact]
    public void ThePlan_LinksEachItemUnderItsFoldersNode_ByTheTreesLinkType()
    {
        var parent = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Projects", _clock.Now);
        _graph.Add(parent);
        File(Path.Combine("Trip", "Days", "monday.md"), 4);
        var preview = _planner.Preview([_temp.Sub("Trip")], new ParentLink(parent, TypeCatalog.RelatesTo.Id));

        var plan = _planner.Plan(preview);

        var nodes = plan.Changes.OfType<NodeAdded>().Select(added => added.Node).ToList();
        Assert.Equal([TypeCatalog.Generic.Id, TypeCatalog.Generic.Id, TypeCatalog.Dataset.Id], nodes.Select(node => node.Type));
        var links = plan.Changes.OfType<LinkAdded>().Select(added => added.Link).ToList();
        Assert.Equal([(parent.Id, nodes[0].Id), (nodes[0].Id, nodes[1].Id), (nodes[1].Id, nodes[2].Id)], links.Select(link => (link.From, link.To)));
        Assert.All(links, link => Assert.Equal(TypeCatalog.RelatesTo.Id, link.Type));
        Assert.Equal([nodes[2]], plan.Files.Select(file => file.Node));
    }

    [Fact]
    public void WithoutAParent_AFoldersContent_IsLinkedByContains_AndTheFolderByNothing()
    {
        File(Path.Combine("Trip", "a.txt"), 1);

        var plan = _planner.Plan(_planner.Preview([_temp.Sub("Trip")]));

        var nodes = plan.Changes.OfType<NodeAdded>().Select(added => added.Node).ToList();
        var link = Assert.Single(plan.Changes.OfType<LinkAdded>()).Link;
        Assert.Equal((TypeCatalog.Contains.Id, nodes[0].Id, nodes[1].Id), (link.Type, link.From, link.To));
    }

    [Fact]
    public void WithoutAParent_ANameTakenInTheVault_IsAHomonym_AndFilesOfTheSameName_KeepTheirExtension()
    {
        _graph.Add(new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Notes", _clock.Now));

        var preview = _planner.Preview([File("notes.txt", 1), File("report.pdf", 1), File("Report.docx", 1)]);

        Assert.Equal(["notes", "report.pdf", "Report.docx"], preview.Rows.Select(row => row.NodeName));
        Assert.Equal([true, false, false], preview.Rows.Select(row => row.IsHomonym));
    }

    [Fact]
    public void InAFolder_AFileNamedAsAFolder_KeepsItsExtension()
    {
        Directory.CreateDirectory(_temp.Sub(Path.Combine("Trip", "report")));
        File(Path.Combine("Trip", "report.pdf"), 1);

        var preview = _planner.Preview([_temp.Sub("Trip")]);

        Assert.Equal(["Trip", "report", "report.pdf"], preview.Rows.Select(row => row.NodeName));
        Assert.All(preview.Rows, row => Assert.False(row.IsHomonym));
    }

    [Fact]
    public void FilesGiven_OfTheSameName_KeepTheirExtension()
    {
        var preview = _planner.Preview([Given("a.txt"), Given("A.csv")]);

        Assert.Equal(["a.txt", "A.csv"], preview.Rows.Select(row => row.NodeName));
        Assert.All(preview.Rows, row => Assert.False(row.IsHomonym));
    }

    [Fact]
    public void FilesGiven_TheSameTwice_AreHomonyms()
    {
        var preview = _planner.Preview([Given("a.txt"), Given("a.txt")]);

        Assert.Equal([false, true], preview.Rows.Select(row => row.IsHomonym));
    }

    [Fact]
    public void FilesGiven_UnderATreeNode_UpdateTheirDataset()
    {
        var parent = AddNode("Projects", TypeCatalog.Generic.Id);
        var dataset = AddUnder(parent, AddNode("a", TypeCatalog.Dataset.Id));
        AddNode("b", TypeCatalog.Generic.Id);

        var preview = _planner.Preview([Given("a.txt"), Given("b.txt")], new ParentLink(parent, TypeCatalog.Contains.Id));

        Assert.Equal([dataset, null], preview.Rows.Select(row => row.Existing));
        Assert.All(preview.Rows, row => Assert.False(row.IsHomonym));
    }

    private static ImportFile Given(string name) => new(name, 1, () => Stream.Null);

    [Fact]
    public void UnderATreeNode_OnlyANameTakenUnderIt_IsAHomonym()
    {
        var parent = AddNode("Projects", TypeCatalog.Generic.Id);
        AddNode("a", TypeCatalog.Generic.Id);
        AddUnder(parent, AddNode("b", TypeCatalog.Generic.Id));

        var preview = _planner.Preview([File("a.txt", 1), File("b.txt", 1)], new ParentLink(parent, TypeCatalog.Contains.Id));

        Assert.Equal([false, true], preview.Rows.Select(row => row.IsHomonym));
        Assert.All(preview.Rows, row => Assert.Null(row.Existing));
        Assert.Equal("Add 2 files · 2 bytes", preview.Heading);
    }

    [Fact]
    public void UnderATreeNode_AFileUpdatesItsDataset_AndAFolderReusesItsNode_TheOldest()
    {
        var parent = AddNode("Projects", TypeCatalog.Generic.Id);
        var dataset = AddUnder(parent, AddNode("a", TypeCatalog.Dataset.Id));
        var trip = AddUnder(parent, AddNode("Trip", TypeCatalog.Generic.Id));
        _clock.Advance(TimeSpan.FromMinutes(1));
        AddUnder(parent, AddNode("trip", TypeCatalog.Generic.Id));
        var inTrip = AddUnder(trip, AddNode("b", TypeCatalog.Dataset.Id));
        File("a.txt", 3);
        File(Path.Combine("Trip", "b.txt"), 4);
        File(Path.Combine("Trip", "c.txt"), 5);

        var preview = _planner.Preview([_temp.Sub("a.txt"), _temp.Sub("Trip")], new ParentLink(parent, TypeCatalog.Contains.Id));

        Assert.Equal([dataset, trip, inTrip, null], preview.Rows.Select(row => row.Existing));
        Assert.Equal([true, false, true, false], preview.Rows.Select(row => row.IsUpdate));
        Assert.All(preview.Rows, row => Assert.False(row.IsHomonym));
        Assert.Equal((1, 0, 2, 3), (preview.FileCount, preview.FolderCount, preview.UpdateCount, preview.Count));
        Assert.Equal("Add 1 file, update 2 files · 12 bytes", preview.Heading);

        var plan = _planner.Plan(preview);

        var added = Assert.Single(plan.Changes.OfType<NodeAdded>()).Node;
        var link = Assert.Single(plan.Changes.OfType<LinkAdded>()).Link;
        Assert.Equal((trip.Id, added.Id), (link.From, link.To));
        Assert.Equal([added], plan.Files.Select(file => file.Node));
        Assert.Equal([(dataset, "a.txt"), (inTrip, "b.txt")], plan.Updates.Select(update => (update.Node, update.File.Name)));
    }

    [Fact]
    public void AFileNamedAsAnotherKindOfNode_UnderTheSameParent_IsANewHomonym()
    {
        var parent = AddNode("Projects", TypeCatalog.Generic.Id);
        AddUnder(parent, AddNode("a", TypeCatalog.Generic.Id));
        AddUnder(parent, AddNode("Trip", TypeCatalog.Dataset.Id));
        Directory.CreateDirectory(_temp.Sub("Trip"));

        var preview = _planner.Preview([File("a.txt", 1), _temp.Sub("Trip")], new ParentLink(parent, TypeCatalog.Contains.Id));

        Assert.All(preview.Rows, row => Assert.Null(row.Existing));
        Assert.All(preview.Rows, row => Assert.True(row.IsHomonym));
    }

    [Fact]
    public void OnlyUpdates_AreHeadedAsSuch()
    {
        var parent = AddNode("Projects", TypeCatalog.Generic.Id);
        AddUnder(parent, AddNode("a", TypeCatalog.Dataset.Id));

        Assert.Equal("Update 1 file · 5 bytes", _planner.Preview([File("a.txt", 5)], new ParentLink(parent, TypeCatalog.Contains.Id)).Heading);
    }

    private Node AddNode(string name, string type)
    {
        var node = new Node(Guid.NewGuid(), type, name, _clock.Now);
        _graph.Add(node);
        return node;
    }

    private Node AddUnder(Node parent, Node child)
    {
        _graph.Add(new Link(Guid.NewGuid(), TypeCatalog.Contains.Id, parent.Id, child.Id, _clock.Now));
        return child;
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
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllBytes(path, new byte[size]);
        return path;
    }
}
