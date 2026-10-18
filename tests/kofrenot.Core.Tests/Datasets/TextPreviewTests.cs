using System.Text;

namespace Kofrenot.Core.Tests.Datasets;

public sealed class TextPreviewTests
{
    [Fact]
    public void AShortText_IsShownWhole() =>
        Assert.Equal(new TextPreview("{ \"été\": 1 }", false), TextPreview.Read(Utf8("{ \"été\": 1 }")));

    [Fact]
    public void ALongText_IsCut()
    {
        Assert.Equal(new TextPreview("abc", true), TextPreview.Read(Utf8("abcd"), maxChars: 3));
        Assert.Equal(new TextPreview("abc", false), TextPreview.Read(Utf8("abc"), maxChars: 3));
    }

    [Fact]
    public void AByteOrderMark_GivesTheEncoding() =>
        Assert.Equal("été", TextPreview.Read(new MemoryStream([.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("été")])).Text);

    private static MemoryStream Utf8(string text) => new(Encoding.UTF8.GetBytes(text));
}
