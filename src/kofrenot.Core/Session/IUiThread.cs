namespace Kofrenot.Core.Session;

/// <summary>
/// The thread a client's UI runs on. The session raises its events there and starts its syncs
/// there, so a client shows them as they come, from one thread.
/// </summary>
public interface IUiThread
{
    /// <summary>Runs <paramref name="action"/> on the UI thread, later.</summary>
    void Post(Action action);
}
