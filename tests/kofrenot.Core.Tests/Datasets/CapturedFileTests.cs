namespace Kofrenot.Core.Tests.Datasets;

public sealed class CapturedFileTests
{
    private static readonly DateTime Taken = new(2026, 10, 28, 20, 30, 45);

    [Theory]
    [InlineData(CaptureKind.Photo, "Photo 2026-10-28 20.30.jpg")]
    [InlineData(CaptureKind.Video, "Video 2026-10-28 20.30.mp4")]
    public void ACapture_IsNamedAfterWhenItWasTaken(CaptureKind kind, string name) =>
        Assert.Equal(name, CapturedFile.NameOf(kind, Taken));

    [Fact]
    public void ItsDataset_IsNamedWithoutTheExtension_AndKnowsItsKind()
    {
        var name = CapturedFile.NameOf(CaptureKind.Photo, Taken);

        Assert.Equal("Photo 2026-10-28 20.30", FileImportPlanner.NodeNameOf(name));
        Assert.Equal(DatasetViewer.Image, DatasetType.Of(new DatasetFile(name, 1, "00")).Viewer);
        Assert.Equal("mp4", DatasetType.Of(new DatasetFile(CapturedFile.NameOf(CaptureKind.Video, Taken), 1, "00")).Extension);
    }
}
