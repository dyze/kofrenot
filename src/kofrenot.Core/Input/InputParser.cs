using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// Reads the text of the input box. The first phrase of a link type, either way ("contains",
/// "contained by"), as whole words after at least one word, makes it a link; otherwise it is a node. Words are
/// compared as names are (<see cref="NameKey"/>), and names come out with single spaces.
/// </summary>
public static class InputParser
{
    /// <summary>The request, or null for an empty text.</summary>
    public static InputRequest? Parse(string text, TypeCatalog? types = null)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return null;
        }

        var keys = words.Select(NameKey.Of).ToArray();
        var phrases = (types ?? TypeCatalog.BuiltIn).Verbs
            .SelectMany(verb => verb.Phrases.Select(phrase => (Verb: verb, Words: NameKey.Of(phrase).Split(' '))))
            // At one place, the longest phrase wins.
            .OrderByDescending(phrase => phrase.Words.Length)
            .ToList();

        for (var start = 1; start < words.Length; start++)
        {
            var phrase = phrases.FirstOrDefault(candidate => StartsAt(keys, start, candidate.Words));
            if (phrase.Verb is not null)
            {
                return new LinkRequest(Join(words[..start]), phrase.Verb, Join(words[(start + phrase.Words.Length)..]));
            }
        }

        return new NodeRequest(Join(words));
    }

    private static bool StartsAt(string[] keys, int start, string[] phrase) =>
        start + phrase.Length <= keys.Length && phrase.Select((word, index) => keys[start + index] == word).All(same => same);

    private static string Join(string[] words) => string.Join(' ', words);
}
