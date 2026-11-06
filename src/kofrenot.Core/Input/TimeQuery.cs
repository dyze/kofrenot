using Kofrenot.Core.Fields;

namespace Kofrenot.Core.Input;

/// <summary>What a time query compares: when a node last changed, was added, or was last opened on this device.</summary>
public enum TimeKind
{
    Changed,
    Added,
    Opened,
}

/// <summary>
/// <c>changed: this week</c>, <c>added: 2025-08</c>, <c>opened: today</c>: the nodes by when, in a period of days:
/// <c>today</c>, <c>yesterday</c>, <c>this week</c> (from Monday), <c>this month</c>, <c>this year</c>, a day, a month
/// or a year as a date field takes them, or a range of those (<c>2025-01..2025-03</c>, either end left open). No period: any time.
/// </summary>
public sealed record TimeQuery(TimeKind Kind, string Period)
{
    public const string Hint = "A period is today, yesterday, this week, this month, this year, a date (2025-08-14), a month (2025-08), a year (2025) or a range (2025-01..2025-03).";

    private const string Range = "..";

    /// <summary>The word typed before <c>:</c>, as it reads in a row's detail (<c>changed 3 days ago</c>).</summary>
    public string Word => Kind.ToString().ToLowerInvariant();

    /// <summary>The kind a word names (<c>changed</c>, <c>added</c>, <c>opened</c>), or null.</summary>
    public static TimeKind? KindOf(string word) =>
        Enum.GetValues<TimeKind>().Cast<TimeKind?>().FirstOrDefault(kind => kind.ToString()!.Equals(word.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The first and last days of the period, both included, null when open; null when it is no period.</summary>
    public (DateOnly? From, DateOnly? To)? DaysAt(DateOnly today)
    {
        var period = string.Join(' ', InputParser.Words(Period.ToLowerInvariant()));
        switch (period)
        {
            case "":
                return (null, null);
            case "today":
                return (today, today);
            case "yesterday":
                return (today.AddDays(-1), today.AddDays(-1));
            case "this week":
                return (today.AddDays(-(((int)today.DayOfWeek + 6) % 7)), today);
            case "this month":
                return (new DateOnly(today.Year, today.Month, 1), today);
            case "this year":
                return (new DateOnly(today.Year, 1, 1), today);
        }

        if (period.Split(Range) is [var from, var to])
        {
            var (first, last) = (DaysOf(from), DaysOf(to));
            return (from.Trim().Length > 0 && first is null) || (to.Trim().Length > 0 && last is null) || (first is null && last is null)
                ? null
                : (first?.From, last?.To);
        }

        return DaysOf(period) is { } days ? (days.From, days.To) : null;
    }

    // A day, a month or a year, as a date field takes it: its first and last days.
    private static (DateOnly From, DateOnly To)? DaysOf(string typed)
    {
        if (typed.Trim().Length == 0 || FieldValue.Parse(FieldKind.Date, typed) is not { IsValid: true, Value: { } value }
            || FieldValue.FirstDayOf(value) is not { } first)
        {
            return null;
        }

        return value.Length switch
        {
            4 => (first, first.AddYears(1).AddDays(-1)),
            7 => (first, first.AddMonths(1).AddDays(-1)),
            _ => (first, first),
        };
    }
}
