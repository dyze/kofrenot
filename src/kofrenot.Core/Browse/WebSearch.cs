namespace Kofrenot.Core.Browse;

/// <summary>The search engine a node's name is searched with on the web (Settings > General). Google by default.</summary>
public enum WebSearchEngine
{
    Google,
    DuckDuckGo,
    Bing,
}

/// <summary>
/// A node's name searched on the web, in the default browser: the only time a name leaves the device,
/// and only when the user asks. On Android the phone's own search app does it instead, when it has one.
/// </summary>
public static class WebSearch
{
    /// <summary>The engine's results for <paramref name="text"/>, its words encoded; null for a text with no words.</summary>
    public static Uri? UrlOf(WebSearchEngine engine, string text)
    {
        var words = string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (words.Length == 0)
        {
            return null;
        }

        var query = Uri.EscapeDataString(words);
        return new Uri(engine switch
        {
            WebSearchEngine.DuckDuckGo => $"https://duckduckgo.com/?q={query}",
            WebSearchEngine.Bing => $"https://www.bing.com/search?q={query}",
            _ => $"https://www.google.com/search?q={query}",
        });
    }
}
