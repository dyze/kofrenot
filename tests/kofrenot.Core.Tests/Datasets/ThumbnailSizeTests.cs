namespace Kofrenot.Core.Tests.Datasets;

public sealed class ThumbnailSizeTests
{
    [Theory]
    [InlineData(1334, 2000, 256, 384)]
    [InlineData(4000, 3000, 341, 256)]
    [InlineData(1000, 1000, 256, 256)]
    [InlineData(6000, 1000, 512, 85)]
    [InlineData(200, 100, 200, 100)]
    [InlineData(300, 100, 300, 100)]
    [InlineData(3000, 1, 512, 1)]
    public void TheShorterSideReaches256_TheLongerAtMost512_NeverEnlarged(int width, int height, int thumbnailWidth, int thumbnailHeight) =>
        Assert.Equal((thumbnailWidth, thumbnailHeight), ThumbnailSize.Of(width, height));
}
