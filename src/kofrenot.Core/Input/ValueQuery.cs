using System.Globalization;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>
/// A value searched in a field: words starting the value shown, a word starting after any sign
/// (<c>isfdb</c> finds <c>https://isfdb.org</c>),
/// or for a date or a number a range, either end left open (<c>1960..1970</c>, <c>1965-08..</c>, <c>..12.5</c>).
/// A date's ends are a year, a month or a day (<c>1960</c>, <c>1960-05</c>, <c>1960-05-01</c>), each included whole.
/// </summary>
public static class ValueQuery
{
    private const string Range = "..";

    /// <summary>Whether <paramref name="typed"/> is a range: two ends around <c>..</c>, at least one typed.</summary>
    public static bool IsRange(string typed) =>
        typed.Split(Range) is [var from, var to] && (from.Trim().Length > 0 || to.Trim().Length > 0);

    /// <summary>Whether a field's value matches what is typed; an empty text matches any value.</summary>
    public static bool Matches(NodeField field, string typed)
    {
        if (field.Value is not { } value)
        {
            return false;
        }

        typed = typed.Trim();
        if (typed.Length == 0)
        {
            return true;
        }

        if (IsRange(typed))
        {
            var ends = typed.Split(Range).Select(end => end.Trim()).ToArray();
            return field.Kind switch
            {
                FieldKind.Date => InDates(value, ends[0], ends[1]),
                FieldKind.Number => InNumbers(value, ends[0], ends[1]),
                _ => false,
            };
        }

        return StartsAWord(NameKey.Of(field.Display), NameKey.Of(typed));
    }

    // Whether the query starts a word of the key: after a space, or any sign ("isfdb" in "https://isfdb.org").
    private static bool StartsAWord(string key, string query)
    {
        for (var index = key.IndexOf(query, StringComparison.Ordinal); index >= 0; index = key.IndexOf(query, index + 1, StringComparison.Ordinal))
        {
            if (index == 0 || !char.IsLetterOrDigit(key[index - 1]))
            {
                return true;
            }
        }

        return false;
    }

    // ISO dates compare as text; an end of a year or a month includes all of it.
    private static bool InDates(string value, string from, string to) =>
        (from.Length == 0 || (IsDateEnd(from) && string.CompareOrdinal(value[..Math.Min(from.Length, value.Length)], from) >= 0))
        && (to.Length == 0 || (IsDateEnd(to) && string.CompareOrdinal(value[..Math.Min(to.Length, value.Length)], to) <= 0));

    private static bool InNumbers(string value, string from, string to) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
        && (from.Length == 0 || (NumberOf(from) is { } low && number >= low))
        && (to.Length == 0 || (NumberOf(to) is { } high && number <= high));

    private static decimal? NumberOf(string typed) =>
        FieldValue.Parse(FieldKind.Number, typed) is { IsValid: true, Value: { } value } ? decimal.Parse(value, CultureInfo.InvariantCulture) : null;

    // A day, a month or a year, as a date field writes it.
    private static bool IsDateEnd(string typed) => FieldValue.IsValid(FieldKind.Date, typed);
}
