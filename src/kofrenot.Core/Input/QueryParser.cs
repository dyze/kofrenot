using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// Reads a query in the input box (<see cref="NodeQuery"/>): <c>?</c> first (<see cref="Mark"/>), then a node type's name
/// and <c>:</c> and a name, a time (<c>changed:</c>, <c>added:</c> or <c>opened:</c> and a period, <see cref="TimeQuery"/>),
/// or a link phrase and a node; anything else after <c>?</c> is a name. A time's word wins over a type of that name.
/// Without <c>?</c>, it is no query: it adds as before (<see cref="InputParser"/>).
/// </summary>
public static class QueryParser
{
    public const char Mark = '?';

    /// <summary>Whether the text is a query: <c>?</c> first.</summary>
    public static bool IsQuery(string text) => text.TrimStart().StartsWith(Mark);

    /// <summary>The text after <c>?</c>, as typed.</summary>
    internal static string AfterMark(string text) => text.TrimStart()[1..];

    /// <summary>The query, or null when the text is none.</summary>
    public static NodeQuery? Parse(string text, TypeCatalog types)
    {
        if (!IsQuery(text))
        {
            return null;
        }

        var rest = AfterMark(text).Trim();
        NodeType? type = null;
        if (TimeOf(rest) is null && TypeOf(rest, types) is { } typed)
        {
            (type, rest) = typed;
        }

        if (TimeOf(rest) is { } time)
        {
            return new NodeQuery(type, string.Empty, null, time);
        }

        return LinkOf(rest, types) is { } link
            ? new NodeQuery(type, string.Empty, link, null)
            : new NodeQuery(type, rest, null, null);
    }

    internal static (string Before, string After)? Colon(string text) =>
        text.IndexOf(':') is var colon and >= 0 ? (text[..colon], text[(colon + 1)..].Trim()) : null;

    internal static TimeQuery? TimeOf(string text) =>
        Colon(text) is { } parts && TimeQuery.KindOf(parts.Before) is { } kind ? new TimeQuery(kind, parts.After) : null;

    internal static (NodeType Type, string After)? TypeOf(string text, TypeCatalog types) =>
        Colon(text) is { } parts && NameKey.Of(parts.Before) is { Length: > 0 } key
            && types.NodeTypes.FirstOrDefault(type => NameKey.Of(type.Name) == key) is { } type
            ? (type, parts.After)
            : null;

    // A link phrase first, as whole words: the longest one; the node is the words after it.
    internal static LinkQuery? LinkOf(string text, TypeCatalog types)
    {
        var words = InputParser.Words(text);
        var keys = words.Select(NameKey.Of).ToArray();
        return types.Verbs
            .SelectMany(verb => verb.Phrases.Select(phrase => (Verb: verb, Words: NameKey.Of(phrase).Split(' '))))
            .Where(phrase => phrase.Words.Length <= keys.Length && phrase.Words.SequenceEqual(keys[..phrase.Words.Length]))
            .OrderByDescending(phrase => phrase.Words.Length)
            .Select(phrase => new LinkQuery(phrase.Verb, string.Join(' ', words[phrase.Words.Length..])))
            .FirstOrDefault();
    }
}
