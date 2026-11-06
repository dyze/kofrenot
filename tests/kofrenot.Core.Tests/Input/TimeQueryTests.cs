namespace Kofrenot.Core.Tests.Input;

public sealed class TimeQueryTests
{
    // A Friday.
    private static readonly DateOnly Today = new(2026, 11, 6);

    [Theory]
    [InlineData("today", "2026-11-06", "2026-11-06")]
    [InlineData(" Yesterday ", "2026-11-05", "2026-11-05")]
    [InlineData("this  week", "2026-11-02", "2026-11-06")]
    [InlineData("this month", "2026-11-01", "2026-11-06")]
    [InlineData("this year", "2026-01-01", "2026-11-06")]
    [InlineData("2025", "2025-01-01", "2025-12-31")]
    [InlineData("2025-02", "2025-02-01", "2025-02-28")]
    [InlineData("2025-08-14", "2025-08-14", "2025-08-14")]
    [InlineData("2025-01..2025-03", "2025-01-01", "2025-03-31")]
    [InlineData("2025-08..", "2025-08-01", null)]
    [InlineData("..2025", null, "2025-12-31")]
    [InlineData("", null, null)]
    public void APeriod_IsItsDays_BothIncluded(string period, string? from, string? to) =>
        Assert.Equal((Day(from), Day(to)), new TimeQuery(TimeKind.Changed, period).DaysAt(Today));

    [Theory]
    [InlineData("soon")]
    [InlineData("..")]
    [InlineData("soon..2025")]
    [InlineData("2025..soon")]
    public void AnythingElse_IsNoPeriod(string period) =>
        Assert.Null(new TimeQuery(TimeKind.Changed, period).DaysAt(Today));

    [Fact]
    public void TheWords_NameTheKinds()
    {
        Assert.Equal(TimeKind.Added, TimeQuery.KindOf(" Added "));
        Assert.Null(TimeQuery.KindOf("person"));
        Assert.Equal("opened", new TimeQuery(TimeKind.Opened, "").Word);
    }

    private static DateOnly? Day(string? day) => day is null ? null : DateOnly.Parse(day, System.Globalization.CultureInfo.InvariantCulture);
}
