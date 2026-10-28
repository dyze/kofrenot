namespace Kofrenot.Core.Tests.Server;

public sealed class ExchangeStatsTests : IDisposable
{
    private const string Store = "v1/stores/5f0c/files/";

    private readonly TempFolder _temp = new();
    private readonly ManualClock _clock = new();
    private readonly RingLog _log = new();

    public void Dispose() => _temp.Dispose();

    private string FilePath => _temp.Sub("exchanges.json");

    [Theory]
    [InlineData(Store + "changes/ab/abcd.kfc", ExchangeKind.Changes)]
    [InlineData(Store + "content/ab/abcd.kfn", ExchangeKind.Notes)]
    [InlineData(Store + "content/ab/abcd.kfd", ExchangeKind.Files)]
    [InlineData(Store + "content/ab/abcd.kft", ExchangeKind.Thumbnails)]
    [InlineData(Store + "kofrenot-vault.json", ExchangeKind.Sync)]
    [InlineData("v1/stores/5f0c/feed?since=12", ExchangeKind.Sync)]
    [InlineData("v1/sessions/refresh", ExchangeKind.Account)]
    [InlineData("v1/stores/5f0c/members", ExchangeKind.Account)]
    public void ARequest_IsCountedByWhatItExchanges(string address, ExchangeKind kind) =>
        Assert.Equal(kind, ExchangeStats.KindOf(address));

    [Fact]
    public void Requests_AddUp_PerDayAndKind()
    {
        var stats = new ExchangeStats(clock: _clock);

        stats.Record(Store + "content/ab/abcd.kfd", up: 0, down: 2048, failed: false);
        stats.Record(Store + "content/ab/abce.kfd", up: 100, down: 0, failed: true);
        stats.Record("v1/stores/5f0c/feed?since=0", up: 0, down: 10, failed: false);
        _clock.Advance(TimeSpan.FromDays(1));
        stats.Record("v1/sessions", up: 50, down: 5, failed: false);

        var days = stats.Days;
        Assert.Equal(2, days.Count);
        Assert.True(days[0].Day > days[1].Day);
        Assert.Equal(new ExchangeCount(2, 1, 100, 2048), days[1].Kinds[ExchangeKind.Files]);
        Assert.Equal(new ExchangeCount(3, 1, 100, 2058), days[1].Total);
        Assert.Equal(new ExchangeCount(1, 0, 50, 5), days[0].Total);
    }

    [Fact]
    public void OnlyTheLast30Days_AreKept()
    {
        var stats = new ExchangeStats(clock: _clock);
        stats.Record("v1/sessions", 1, 1, failed: false);
        _clock.Advance(TimeSpan.FromDays(ExchangeStats.DaysKept - 1));
        stats.Record("v1/sessions", 1, 1, failed: false);

        Assert.Equal(2, stats.Days.Count);

        _clock.Advance(TimeSpan.FromDays(1));
        Assert.Single(stats.Days);
        stats.Record("v1/sessions", 1, 1, failed: false);
        Assert.Equal(2, stats.Days.Count);
    }

    [Fact]
    public void TheyAreSaved_AndReadAgain_OnlyWhenChanged()
    {
        var stats = new ExchangeStats(FilePath, _clock, _log);
        stats.Save();
        Assert.False(File.Exists(FilePath));

        stats.Record(Store + "changes/ab/abcd.kfc", 300, 0, failed: false);
        stats.Save();

        var again = new ExchangeStats(FilePath, _clock, _log);
        Assert.Equal(new ExchangeCount(1, 0, 300, 0), Assert.Single(again.Days).Kinds[ExchangeKind.Changes]);
        Assert.DoesNotContain("abcd", File.ReadAllText(FilePath), StringComparison.Ordinal);
    }

    [Fact]
    public void ADamagedFile_ReadsAsNone_AndIsLogged()
    {
        Directory.CreateDirectory(_temp.Path);
        File.WriteAllText(FilePath, "{ not json");

        Assert.Empty(new ExchangeStats(FilePath, _clock, _log).Days);
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public void Rows_AndText_ShowEachDay_TheTotal_AndEachKind()
    {
        var stats = new ExchangeStats(clock: _clock);
        stats.Record(Store + "content/ab/abcd.kfn", 0, 2048, failed: false);
        stats.Record("v1/sessions", 10, 0, failed: true);

        var day = Assert.Single(stats.DayRows(), row => row.Label != "Total");
        Assert.Equal((2, 1, "10 bytes", "2.0 KB"), (day.Requests, day.Failed, day.Up, day.Down));
        Assert.Equal("Total", stats.DayRows()[^1].Label);
        Assert.Equal(["Notes", "Account"], stats.KindRows().Select(row => row.Label));
        var text = stats.Text();
        Assert.Contains("last 30 days", text, StringComparison.Ordinal);
        Assert.Contains("By kind", text, StringComparison.Ordinal);
        Assert.Contains("Notes", text, StringComparison.Ordinal);
    }
}
