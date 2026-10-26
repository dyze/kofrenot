using Kofrenot.Core.Logging;

namespace Kofrenot.Core.Session;

/// <summary>
/// A change file skipped because it was locked or still being copied is read again a few seconds
/// later (<see cref="RetryDelays"/>), not only at the next sync event or when the window is shown.
/// </summary>
public sealed partial class VaultSession
{
    /// <summary>The waits before each new try; after the last one, the next refresh tries again.</summary>
    public static readonly IReadOnlyList<TimeSpan> RetryDelays = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];

    private ITimer? _retryTimer;
    private int _retries;

    /// <summary>Plans the next try while the open vault has skipped change files; stops once they are read.</summary>
    private void RetrySkippedChanges()
    {
        if (Current?.HasSkippedChanges != true)
        {
            _retries = 0;
            _retryTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            return;
        }

        if (_retries >= RetryDelays.Count)
        {
            return;
        }

        _retryTimer ??= _time.CreateTimer(_ => _ui.Post(RetryNow), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _retryTimer.Change(RetryDelays[_retries++], Timeout.InfiniteTimeSpan);
    }

    private void RetryNow()
    {
        _log.Debug(Category, "Skipped change files read again");
        Refresh();
    }
}
