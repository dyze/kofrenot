using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// A qualifier typed after a link sentence: <c>, Date 1965</c>. <see cref="Name"/> is the qualifier's name as its
/// link type has it; an empty <see cref="Value"/> takes it out of an existing link.
/// </summary>
public sealed record QualifierInput(string Name, string Value)
{
    public bool IsRemoval => Value.Length == 0;
}

/// <summary>
/// Reads the qualifiers typed after a link sentence (<c>Dune written by Frank Herbert, Date 1965, Source isfdb.org</c>):
/// a <c>,</c> followed by the name of a qualifier of the link type, as whole words, starts one; what follows its
/// name is its value. A <c>,</c> not followed by a qualifier's name stays where it is: in the second node's name
/// (<i>Herbert, Frank</i>), or in the value before it (<i>12 rue de la Paix, Vendôme</i>).
/// </summary>
public static class QualifierText
{
    /// <summary>The request with its qualifiers out of its second node's name; unchanged when none is typed.</summary>
    public static LinkRequest Split(LinkRequest request, IReadOnlyList<FieldDefinition> fields)
    {
        if (fields.Count == 0 || FirstQualifier(request.Right, fields) is not { } start)
        {
            return request;
        }

        var qualifiers = new List<QualifierInput>();
        foreach (var segment in request.Right[(start + 1)..].Split(','))
        {
            if (NameAt(segment, fields) is { } field)
            {
                qualifiers.Add(new QualifierInput(field.Name, Join(InputParser.Words(segment)[InputParser.Words(field.Name).Length..])));
            }
            else
            {
                qualifiers[^1] = qualifiers[^1] with { Value = Join(InputParser.Words(qualifiers[^1].Value + "," + segment)) };
            }
        }

        return request with { Right = request.Right[..start].Trim(), Qualifiers = qualifiers };
    }

    /// <summary>The qualifier whose name starts with the last words typed after a <c>,</c>, longer than them; null when none.</summary>
    /// <param name="afterLinkType">What follows the link type: the second node, and the qualifiers typed so far.</param>
    /// <param name="nodeTyped">The second node is typed before <paramref name="afterLinkType"/> already (a label).</param>
    public static FieldDefinition? Proposed(string afterLinkType, IReadOnlyList<FieldDefinition> fields, bool nodeTyped = false)
    {
        var comma = afterLinkType.LastIndexOf(',');
        if (comma < 0 || (!nodeTyped && afterLinkType[..comma].Trim().Length == 0))
        {
            return null;
        }

        var typed = Join(InputParser.Words(afterLinkType[(comma + 1)..]));
        return typed.Length == 0 || char.IsWhiteSpace(afterLinkType[^1])
            ? null
            : fields.Where(field => field.Name.Length > typed.Length && field.Name.StartsWith(typed, StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(field => field.Name.Length)
                .FirstOrDefault();
    }

    /// <summary>
    /// Where the first qualifier starts in what follows the link type: the index of its <c>,</c>, after a second node; null when none.
    /// With <paramref name="complete"/>, the qualifier's name must be followed by a space: still typed, it is not one yet.
    /// </summary>
    /// <param name="nodeTyped">The second node is typed before <paramref name="afterLinkType"/> already (a label).</param>
    public static int? FirstQualifier(string afterLinkType, IReadOnlyList<FieldDefinition> fields, bool complete = false, bool nodeTyped = false)
    {
        for (var comma = afterLinkType.IndexOf(','); comma >= 0; comma = afterLinkType.IndexOf(',', comma + 1))
        {
            if ((nodeTyped || afterLinkType[..comma].Trim().Length > 0) && NameAt(afterLinkType[(comma + 1)..], fields, complete) is not null)
            {
                return comma;
            }
        }

        return null;
    }

    /// <summary>The qualifier whose name the text starts with, as whole words, the longest first; null when none.</summary>
    internal static FieldDefinition? NameAt(string text, IReadOnlyList<FieldDefinition> fields, bool complete = false)
    {
        var keys = InputParser.Words(text).Select(NameKey.Of).ToArray();
        var ended = text.Length > 0 && char.IsWhiteSpace(text[^1]);
        return fields
            .Select(field => (Field: field, Words: InputParser.Words(field.Name).Select(NameKey.Of).ToArray()))
            .Where(field => field.Words.Length > 0 && field.Words.Length <= keys.Length && field.Words.SequenceEqual(keys[..field.Words.Length])
                && (!complete || field.Words.Length < keys.Length || ended))
            .OrderByDescending(field => field.Words.Length)
            .Select(field => field.Field)
            .FirstOrDefault();
    }

    private static string Join(IEnumerable<string> words) => string.Join(' ', words);
}
