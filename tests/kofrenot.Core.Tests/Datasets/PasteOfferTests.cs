namespace Kofrenot.Core.Tests.Datasets;

public sealed class PasteOfferTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly string _photo;
    private readonly string _report;
    private readonly string _folder;

    public PasteOfferTests()
    {
        _folder = Directory.CreateDirectory(_temp.Sub("Trip")).FullName;
        _photo = Write("photo.jpg", 3);
        _report = Write("report.pdf", 5);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void AnImageField_TakesAnImage_OrOneImageFileCopied()
    {
        Assert.True(PasteOffer.FitsImage(new ClipboardContent([], HasImage: true)));
        Assert.True(PasteOffer.FitsImage(new ClipboardContent([_photo], HasImage: false)));
        Assert.False(PasteOffer.FitsImage(new ClipboardContent([_report], HasImage: true)));
        Assert.False(PasteOffer.FitsImage(new ClipboardContent([_photo, _photo], HasImage: false)));
        Assert.False(PasteOffer.FitsImage(new ClipboardContent([_temp.Sub("gone.jpg")], HasImage: false)));
        Assert.False(PasteOffer.FitsImage(ClipboardContent.Empty));
    }

    [Fact]
    public void ADataset_TakesAnImage_OrOneFileCopied_AsItsNewVersion()
    {
        Assert.True(PasteOffer.FitsVersion(new ClipboardContent([], HasImage: true)));
        Assert.True(PasteOffer.FitsVersion(new ClipboardContent([_report], HasImage: false)));
        Assert.False(PasteOffer.FitsVersion(new ClipboardContent([_report, _photo], HasImage: false)));
        Assert.False(PasteOffer.FitsVersion(new ClipboardContent([_folder], HasImage: false)));
        Assert.False(PasteOffer.FitsVersion(ClipboardContent.Empty));
    }

    [Fact]
    public void NewDatasets_TakeFiles_AndAnImportFoldersToo()
    {
        var files = new ClipboardContent([_report, _photo], HasImage: false);
        var withFolder = new ClipboardContent([_report, _folder], HasImage: false);

        Assert.True(PasteOffer.FitsFiles(files));
        Assert.True(PasteOffer.FitsFiles(new ClipboardContent([], HasImage: true)));
        Assert.False(PasteOffer.FitsFiles(withFolder));
        Assert.False(PasteOffer.FitsFiles(ClipboardContent.Empty));
        Assert.True(PasteOffer.FitsImport(withFolder));
        Assert.True(PasteOffer.FitsImport(new ClipboardContent([], HasImage: true)));
        Assert.False(PasteOffer.FitsImport(ClipboardContent.Empty));
    }

    [Fact]
    public void WhatIsRead_IsTheFilesCopiedFirst_ThenTheImage()
    {
        var image = new ImportFile("Pasted image.png", 4, () => new MemoryStream(new byte[4]));

        var files = PasteOffer.Read(new FakeClipboard(new ClipboardContent([_report], HasImage: true), image))!;
        var pictured = PasteOffer.Read(new FakeClipboard(new ClipboardContent([], HasImage: true), image))!;

        Assert.Equal([_report], files.Paths);
        Assert.Null(files.Image);
        Assert.Same(image, pictured.Image);
        Assert.Null(PasteOffer.Read(new FakeClipboard(new ClipboardContent([], HasImage: true), image: null)));
    }

    [Fact]
    public void TheOneFile_IsTheImage_OrTheOneFileCopied()
    {
        var image = new ImportFile("Pasted image.png", 4, () => new MemoryStream(new byte[4]));

        Assert.Same(image, new Pasted([], image).OneFile);
        Assert.Equal(("report.pdf", 5L), (new Pasted([_report], null).OneFile!.Name, new Pasted([_report], null).OneFile!.Size));
        Assert.Null(new Pasted([_report, _photo], null).OneFile);
        Assert.Null(new Pasted([_folder], null).OneFile);
    }

    [Fact]
    public void ItsPreview_IsTheImport_OfTheImageOrOfThePathsCopied()
    {
        var graph = new KnowledgeGraph();
        var planner = new FileImportPlanner(graph);
        var parent = new Node(Guid.NewGuid(), TypeCatalog.Generic.Id, "Holidays", DateTimeOffset.UnixEpoch);
        graph.Add(parent);
        var under = new ParentLink(parent, TypeCatalog.Contains.Id);
        var image = new ImportFile(PasteOffer.ImageNameOf(new DateTime(2026, 11, 4, 10, 30, 15)), 4, () => new MemoryStream(new byte[4]));

        var pictured = new Pasted([], image).Preview(planner, under);
        var copied = new Pasted([_report, _folder], null).Preview(planner, null);

        Assert.Equal(["Pasted image 2026-11-04 10.30.15"], pictured.Rows.Select(row => row.NodeName));
        Assert.Same(under, pictured.Under);
        Assert.Equal(["report", "Trip"], copied.Rows.Select(row => row.NodeName));
    }

    [Fact]
    public void APastedImage_IsNamedAfterWhenItWasPasted_ToTheSecond()
    {
        var name = PasteOffer.ImageNameOf(new DateTime(2026, 11, 4, 10, 30, 15));

        Assert.Equal("Pasted image 2026-11-04 10.30.15.png", name);
        Assert.Equal(DatasetViewer.Image, DatasetType.Of(name).Viewer);
    }

    private string Write(string name, int size)
    {
        var path = _temp.Sub(name);
        File.WriteAllBytes(path, new byte[size]);
        return path;
    }

    private sealed class FakeClipboard(ClipboardContent content, ImportFile? image) : IClipboard
    {
        public event Action? Changed
        {
            add { }
            remove { }
        }

        public ClipboardContent Content => content;

        public ImportFile? ReadImage() => image;
    }
}
