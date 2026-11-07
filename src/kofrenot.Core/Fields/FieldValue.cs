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
/// whatever its language: a number as "1234.5", a date as "2026-10-27", or as vague as known, its
/// month "2026-10" or its year "2026" (since format 11), a checkbox as "true" or "false", a choice as one of its options.
/// </summary>
public static class FieldValue
{
    private const string DateFormat = "yyyy-MM-dd";

    // A date as written: a day, a month or a year, the most precise first.
    private static readonly string[] DateFormats = [DateFormat, "yyyy-MM", "yyyy"];

    // A month typed in the user's way: 10/2026, 10.2026, 10-2026.
    private static readonly string[] MonthFormats = ["M/yyyy", "M.yyyy", "M-yyyy"];

    private const string DateHint = "Type a date, like 2026-10-27, or a month (2026-10) or a year (2026)";
    private const string TelScheme = "tel:";
    private const int MinPhoneDigits = 3;
    private const string PhoneExample = "+33 6 12 34 56 78";
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
            FieldKind.Email => ParseEmail(typed),
            FieldKind.Address => FieldInput.Of(typed),
            FieldKind.Phone => ParsePhone(typed),
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
        FieldKind.Text or FieldKind.Address or FieldKind.Choice => value.Length > 0,
        FieldKind.Number => ParseNumber(value, CultureInfo.InvariantCulture).Value == value,
        FieldKind.Date => DateFormats.Any(format => IsWritten(value, format)),
        FieldKind.Url => ParseUrl(value).Value == value,
        FieldKind.Email => ParseEmail(value).Value == value,
        FieldKind.Phone => ParsePhone(value).Value == value,
        FieldKind.Checkbox => value is True or False,
        _ => false,
    };

    /// <summary>
    /// What a value opens: a web address itself, an email address a new mail to it (<c>mailto:</c>), a
    /// postal address its place on a map (a Google Maps search), a phone number a call to it (<c>tel:</c>,
    /// its digits and leading +); null for the other kinds.
    /// </summary>
    public static Uri? LinkOf(FieldKind kind, string value) => kind switch
    {
        FieldKind.Url when Uri.TryCreate(value, UriKind.Absolute, out var url) => url,
        FieldKind.Email => new Uri($"mailto:{value}"),
        FieldKind.Address => new Uri($"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(value)}"),
        FieldKind.Phone => new Uri($"{TelScheme}{DialedOf(value)}"),
        _ => null,
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

    /// <summary>
    /// The first day of a date's value ("2026-10" is 2026-10-01, "2026" 2026-01-01), where a calendar opens; null
    /// for none, or a value that is no date.
    /// </summary>
    public static DateOnly? FirstDayOf(string? value) =>
        value is not null && DateFormats.FirstOrDefault(format => IsWritten(value, format)) is { } written
            ? DateOnly.ParseExact(value, written, CultureInfo.InvariantCulture)
            : null;

    // As written in the vault: a day, a month or a year, ISO; then a month typed in the user's way
    // (before a whole date, which would read "10/2026" as a day); then a day in the user's way.
    private static FieldInput ParseDate(string typed, CultureInfo culture)
    {
        if (DateFormats.FirstOrDefault(format => IsWritten(typed, format)) is { } written)
        {
            return FieldInput.Of(typed);
        }

        if (DateOnly.TryParseExact(typed, MonthFormats, culture, DateTimeStyles.None, out var month)
            || DateOnly.TryParseExact(typed, culture.DateTimeFormat.YearMonthPattern, culture, DateTimeStyles.None, out month))
        {
            return FieldInput.Of(month.ToString("yyyy-MM", CultureInfo.InvariantCulture));
        }

        return DateOnly.TryParse(typed, culture, DateTimeStyles.None, out var date)
            ? FieldInput.Of(date.ToString(DateFormat, CultureInfo.InvariantCulture))
            : FieldInput.Refused(DateHint);
    }

    // Whether a text is a date written in this format, as it would be written back ("2026-1" is not "2026-01").
    private static bool IsWritten(string text, string format) =>
        DateOnly.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
        && date.ToString(format, CultureInfo.InvariantCulture) == text;

    // An address with its scheme, or a web address without it ("example.com" is https://example.com/).
    private static FieldInput ParseUrl(string typed)
    {
        if (!typed.Any(char.IsWhiteSpace))
        {
            if (Uri.TryCreate(typed, UriKind.Absolute, out var url) && url.Scheme != Uri.UriSchemeFile && !typed.StartsWith('/'))
            {
                return FieldInput.Of(url.OriginalString);
            }

            if (typed.Contains('.') && !typed.Contains(':') && !typed.Contains('@') && Uri.TryCreate($"https://{typed}", UriKind.Absolute, out var web))
            {
                return FieldInput.Of(web.OriginalString);
            }
        }

        return FieldInput.Refused(LooksLikeEmail(typed)
            ? "An email address goes in an email field: type a web address, like https://example.com"
            : "Type an address, like https://example.com");
    }

    // An email address, as typed or after mailto: ("mailto:ann@example.com" is ann@example.com).
    private static FieldInput ParseEmail(string typed)
    {
        var address = typed.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? typed["mailto:".Length..] : typed;
        return LooksLikeEmail(address) ? FieldInput.Of(address) : FieldInput.Refused("Type an email address, like ann@example.com");
    }

    // One @, a name before it, a domain with a dot after it, no spaces.
    private static bool LooksLikeEmail(string text) =>
        text.Split('@') is [{ Length: > 0 } name, { Length: > 0 } domain] && !text.Any(char.IsWhiteSpace) && !name.Contains(':')
        && domain.Contains('.') && !domain.StartsWith('.') && !domain.EndsWith('.');

    // A phone number as typed, or after tel: ("tel:+33 6 12" is +33 6 12): digits, a leading +, and the
    // separators people write (spaces, - . / and brackets); at least 3 digits.
    private static FieldInput ParsePhone(string typed)
    {
        var number = typed.StartsWith(TelScheme, StringComparison.OrdinalIgnoreCase) ? typed[TelScheme.Length..].Trim() : typed;
        if (number.Count(char.IsAsciiDigit) >= MinPhoneDigits && number.LastIndexOf('+') <= 0 && number.All(IsPhoneCharacter))
        {
            return FieldInput.Of(number);
        }

        return FieldInput.Refused(LooksLikeEmail(typed)
            ? $"An email address goes in an email field: type a phone number, like {PhoneExample}"
            : $"Type a phone number, like {PhoneExample}");
    }

    private static bool IsPhoneCharacter(char character) =>
        char.IsAsciiDigit(character) || character is '+' or ' ' or '-' or '.' or '/' or '(' or ')';

    // What is dialed: the digits, and the + of an international number.
    private static string DialedOf(string number) =>
        string.Concat(number.Where(character => char.IsAsciiDigit(character) || character == '+'));

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
