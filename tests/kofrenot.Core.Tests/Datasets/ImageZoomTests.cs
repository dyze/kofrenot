using System.Globalization;

namespace Kofrenot.Core.Tests.Datasets;

public sealed class ImageZoomTests
{
    [Theory]
    [InlineData(800, 600, 1600, 600, 0.5)]
    [InlineData(800, 600, 800, 1200, 0.5)]
    [InlineData(800, 600, 100, 100, 1)]
    [InlineData(800, 600, 1_000_000, 10, ImageZoom.Min)]
    [InlineData(0, 600, 100, 100, 1)]
    [InlineData(800, 600, 0, 100, 1)]
    public void Fit_ShowsTheWholeImage_NeverEnlarged(double areaWidth, double areaHeight, int imageWidth, int imageHeight, double zoom) =>
        Assert.Equal(zoom, ImageZoom.Fit(areaWidth, areaHeight, imageWidth, imageHeight));

    [Fact]
    public void EachStep_MultipliesOrDivides_WithinTheLimits()
    {
        Assert.Equal(1.25, ImageZoom.In(1));
        Assert.Equal(0.8, ImageZoom.Out(1));
        Assert.Equal(ImageZoom.Max, ImageZoom.In(15));
        Assert.Equal(ImageZoom.Min, ImageZoom.Out(0.06));
    }

    [Fact]
    public void TheZoom_IsShownInPercent() =>
        Assert.Equal("156 %", ImageZoom.Percent(1.5625, CultureInfo.InvariantCulture));
}
