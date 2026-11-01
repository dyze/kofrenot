namespace Kofrenot.Core.Tests.Browse;

/// <summary>A node's name as a web search: each engine's address, the words encoded.</summary>
public sealed class WebSearchTests
{
    [Theory]
    [InlineData(WebSearchEngine.Google, "https://www.google.com/search?q=Frank%20Herbert")]
    [InlineData(WebSearchEngine.DuckDuckGo, "https://duckduckgo.com/?q=Frank%20Herbert")]
    [InlineData(WebSearchEngine.Bing, "https://www.bing.com/search?q=Frank%20Herbert")]
    public void AName_IsSearched_WithTheEngineChosen(WebSearchEngine engine, string expected)
    {
        Assert.Equal(expected, WebSearch.UrlOf(engine, "  Frank   Herbert ")!.AbsoluteUri);
    }

    [Fact]
    public void ItsWords_AreEncoded_AmpersandsAndAccentsIncluded()
    {
        var url = WebSearch.UrlOf(WebSearchEngine.Google, "Tom & Jérôme #1?")!;

        Assert.Equal("https://www.google.com/search?q=Tom%20%26%20J%C3%A9r%C3%B4me%20%231%3F", url.AbsoluteUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ANameWithoutWords_IsNotSearched(string name)
    {
        Assert.Null(WebSearch.UrlOf(WebSearchEngine.Google, name));
    }
}
