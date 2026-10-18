using System.Globalization;

namespace Kofrenot.Core.Tests.Datasets;

public sealed class ByteSizeTests
{
    [Theory]
    [InlineData(0, "0 bytes")]
    [InlineData(1, "1 byte")]
    [InlineData(1023, "1023 bytes")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048524, "1023.9 KB")]
    [InlineData(1048525, "1.0 MB")]
    [InlineData(5L * 1024 * 1024 * 1024, "5.0 GB")]
    [InlineData(2048L * 1024 * 1024 * 1024 * 1024, "2048.0 TB")]
    public void ASize_IsShownInTheLargestUnitBelow1024(long bytes, string shown) =>
        Assert.Equal(shown, ByteSize.Format(bytes, CultureInfo.InvariantCulture));

    [Fact]
    public void TheCulture_GivesTheDecimalSeparator() =>
        Assert.Equal("1,5 KB", ByteSize.Format(1536, CultureInfo.GetCultureInfo("fr-FR")));
}
