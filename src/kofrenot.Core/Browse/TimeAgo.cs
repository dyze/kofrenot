namespace Kofrenot.Core.Browse;

/// <summary>How long ago something happened, in a few words: "just now", "5 min ago", "3 h ago", "yesterday", "4 days ago".</summary>
public static class TimeAgo
{
    public static string Of(DateTimeOffset at, DateTimeOffset now)
    {
        // A time ahead (another device's clock) reads as now.
        var ago = now - at;
        return ago.TotalMinutes switch
        {
            < 1 => "just now",
            < 60 => $"{(int)ago.TotalMinutes} min ago",
            < 24 * 60 => $"{(int)ago.TotalHours} h ago",
            < 48 * 60 => "yesterday",
            _ => $"{(int)ago.TotalDays} days ago",
        };
    }
}
