namespace Kofrenot.Core.Tests.Datasets;

public sealed class DatasetTypeTests
{
    [Theory]
    [InlineData("photo.PNG", "png", DatasetCategory.Image, DatasetViewer.Image)]
    [InlineData("scan.jpeg", "jpeg", DatasetCategory.Image, DatasetViewer.Image)]
    [InlineData("cover.webp", "webp", DatasetCategory.Image, DatasetViewer.Image)]
    [InlineData("drawing.svg", "svg", DatasetCategory.Image, DatasetViewer.None)]
    [InlineData("notes.md", "md", DatasetCategory.Text, DatasetViewer.Text)]
    [InlineData("data.json", "json", DatasetCategory.Code, DatasetViewer.Text)]
    [InlineData("page.html", "html", DatasetCategory.Code, DatasetViewer.Text)]
    [InlineData("config.yaml", "yaml", DatasetCategory.Code, DatasetViewer.Text)]
    [InlineData("sound.wav", "wav", DatasetCategory.Audio, DatasetViewer.None)]
    [InlineData("paper.pdf", "pdf", DatasetCategory.Pdf, DatasetViewer.None)]
    [InlineData("README", "", DatasetCategory.Other, DatasetViewer.None)]
    [InlineData("archive.tar.gz", "gz", DatasetCategory.Archive, DatasetViewer.None)]
    public void TheType_IsTheFilesExtension_WithItsCategoryAndViewer(string name, string extension, DatasetCategory category, DatasetViewer viewer) =>
        Assert.Equal(new DatasetType(extension, category, viewer), DatasetType.Of(File(name)));

    [Theory]
    [InlineData("sound.wav", "WAV file")]
    [InlineData("README", "File")]
    public void TheTypeName_IsShown(string name, string shown) =>
        Assert.Equal(shown, DatasetType.Of(File(name)).Name);

    [Fact]
    public void ATextOrAnImageNotTooLarge_IsShown()
    {
        Assert.Null(DatasetType.WhyNotShown(File("data.json", long.MaxValue)));
        Assert.Null(DatasetType.WhyNotShown(File("photo.png", DatasetType.MaxImageSize)));
    }

    [Fact]
    public void AFileWithoutViewer_OrALargeImage_SaysWhy()
    {
        Assert.Equal("No viewer for WAV files: export it to open it.", DatasetType.WhyNotShown(File("sound.wav")));
        Assert.Equal("No viewer for this file: export it to open it.", DatasetType.WhyNotShown(File("README")));
        Assert.StartsWith("Too large to show here", DatasetType.WhyNotShown(File("photo.png", DatasetType.MaxImageSize + 1)));
    }

    [Theory]
    [InlineData("photo.png", 10, true)]
    [InlineData("cover.webp", 10, true)]
    [InlineData("photo.png", DatasetType.MaxImageSize + 1, false)]
    [InlineData("drawing.svg", 10, false)]
    [InlineData("data.json", 10, false)]
    public void OnlyAnImageTheViewerShows_HasAThumbnail(string name, long size, bool hasOne) =>
        Assert.Equal(hasOne, DatasetType.HasThumbnail(File(name, size)));

    private static DatasetFile File(string name, long size = 10) => new(name, size, new string('a', 64));
}
