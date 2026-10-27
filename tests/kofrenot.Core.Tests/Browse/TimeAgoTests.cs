namespace Kofrenot.Core.Tests.Browse;

public sealed class TimeAgoTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(-120, "just now")]
    [InlineData(30, "just now")]
    [InlineData(60, "1 min ago")]
    [InlineData(59 * 60, "59 min ago")]
    [InlineData(60 * 60, "1 h ago")]
    [InlineData(23 * 3600, "23 h ago")]
    [InlineData(30 * 3600, "yesterday")]
    [InlineData(3 * 86400, "3 days ago")]
    public void ATime_ReadsAsHowLongAgo(int secondsAgo, string text) =>
        Assert.Equal(text, TimeAgo.Of(Now.AddSeconds(-secondsAgo), Now));
}
