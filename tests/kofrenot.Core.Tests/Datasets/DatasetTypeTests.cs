namespace Kofrenot.Core.Tests.Datasets;

public sealed class DatasetTypeTests
{
    [Theory]
    [InlineData("photo.PNG", "png", DatasetViewer.Image)]
    [InlineData("scan.jpeg", "jpeg", DatasetViewer.Image)]
    [InlineData("data.json", "json", DatasetViewer.Text)]
    [InlineData("page.html", "html", DatasetViewer.Text)]
    [InlineData("config.yaml", "yaml", DatasetViewer.Text)]
    [InlineData("sound.wav", "wav", DatasetViewer.None)]
    [InlineData("README", "", DatasetViewer.None)]
    [InlineData("archive.tar.gz", "gz", DatasetViewer.None)]
    public void TheType_IsTheFilesExtension_WithItsViewer(string name, string extension, DatasetViewer viewer) =>
        Assert.Equal(new DatasetType(extension, viewer), DatasetType.Of(File(name)));

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

    private static DatasetFile File(string name, long size = 10) => new(name, size, new string('a', 64));
}
