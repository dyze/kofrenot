using System.Globalization;

namespace Kofrenot.Core.Fields;

/// <summary>A value typed for a field: the value to write, or why it does not fit.</summary>
public sealed record FieldInput(string? Value, string? Error)
{
    public bool IsValid => Error is null;

    internal static FieldInput Of(string value) => new(value, null);

    internal static FieldInput Refused(string error) => new(null, error);
}

/// <summary>
/// The values of the small field kinds, written as text in the vault the same way on every device,
/// whatever its language: a number as "1234.5", a date as "2026-10-27", a checkbox as "true" or
/// "false", a choice as one of its options.
/// </summary>
public static class FieldValue
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string True = "true";
    private const string False = "false";

    /// <summary>
    /// What to write for <paramref name="text"/> typed in a field of <paramref name="kind"/>; a
    /// number or a date is also read as written in <paramref name="culture"/> (the current one by default).
    /// </summary>
    public static FieldInput Parse(FieldKind kind, string text, IReadOnlyList<string>? choices = null, CultureInfo? culture = null)
    {
        var typed = text.Trim();
        if (typed.Length == 0)
        {
            return FieldInput.Refused("Type a value");
        }

        culture ??= CultureInfo.CurrentCulture;
        return kind switch
        {
            FieldKind.Text => FieldInput.Of(typed),
            FieldKind.Number => ParseNumber(typed, culture),
            FieldKind.Date => ParseDate(typed, culture),
            FieldKind.Url => ParseUrl(typed),
            FieldKind.Checkbox => ParseCheckbox(typed),
            FieldKind.Choice => ParseChoice(typed, choices ?? []),
            FieldKind.Image => FieldInput.Refused("An image is chosen, not typed in"),
            _ => FieldInput.Refused($"A {FieldKinds.NameOf(kind)} is not typed in"),
        };
    }

    /// <summary>What a checkbox writes.</summary>
    public static string Of(bool isChecked) => isChecked ? True : False;

    /// <summary>Whether a checkbox's value is checked.</summary>
    public static bool IsChecked(string? value) => value == True;

    /// <summary>
    /// Whether <paramref name="value"/>, read from the vault, is one <paramref name="kind"/> writes.
    /// A choice is valid whatever its options: one removed from its field keeps its value.
    /// </summary>
    public static bool IsValid(FieldKind kind, string value) => kind switch
    {
        FieldKind.Text or FieldKind.Choice => value.Length > 0,
        FieldKind.Number => ParseNumber(value, CultureInfo.InvariantCulture).Value == value,
        FieldKind.Date => DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        FieldKind.Url => ParseUrl(value).Value == value,
        FieldKind.Checkbox => value is True or False,
        _ => false,
    };

    /// <summary>How a value is shown, and searched: as written, a checkbox as "Yes" or "No".</summary>
    public static string Display(FieldKind kind, string value) =>
        kind == FieldKind.Checkbox ? (IsChecked(value) ? "Yes" : "No") : value;

    // The culture first: "1,5" is 1.5 in French, not 15.
    private static FieldInput ParseNumber(string typed, CultureInfo culture) =>
        decimal.TryParse(typed, NumberStyles.Number, culture, out var number)
        || decimal.TryParse(typed, NumberStyles.Number, CultureInfo.InvariantCulture, out number)
            ? FieldInput.Of(number.ToString("0.############################", CultureInfo.InvariantCulture))
            : FieldInput.Refused("Type a number, like 1234.5");

    private static FieldInput ParseDate(string typed, CultureInfo culture) =>
        DateOnly.TryParseExact(typed, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
        || DateOnly.TryParse(typed, culture, DateTimeStyles.None, out date)
            ? FieldInput.Of(date.ToString(DateFormat, CultureInfo.InvariantCulture))
            : FieldInput.Refused("Type a date, like 2026-10-27");

    // An address with its scheme, or a web address without it ("example.com" is https://example.com/).
    private static FieldInput ParseUrl(string typed)
    {
        if (!typed.Any(char.IsWhiteSpace))
        {
            if (Uri.TryCreate(typed, UriKind.Absolute, out var url) && url.Scheme != Uri.UriSchemeFile && !typed.StartsWith('/'))
            {
                return FieldInput.Of(url.OriginalString);
            }

            if (typed.Contains('.') && !typed.Contains(':') && Uri.TryCreate($"https://{typed}", UriKind.Absolute, out var web))
            {
                return FieldInput.Of(web.OriginalString);
            }
        }

        return FieldInput.Refused("Type an address, like https://example.com");
    }

    private static FieldInput ParseCheckbox(string typed) => typed.ToLowerInvariant() switch
    {
        True or "yes" or "1" => FieldInput.Of(True),
        False or "no" or "0" => FieldInput.Of(False),
        _ => FieldInput.Refused("Type yes or no"),
    };

    private static FieldInput ParseChoice(string typed, IReadOnlyList<string> choices) =>
        choices.FirstOrDefault(choice => string.Equals(choice, typed, StringComparison.CurrentCultureIgnoreCase)) is { } chosen
            ? FieldInput.Of(chosen)
            : FieldInput.Refused(choices.Count == 0 ? "This field has no options" : $"Choose one of: {string.Join(", ", choices)}");
}
