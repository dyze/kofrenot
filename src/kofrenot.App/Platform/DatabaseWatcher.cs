using System.Windows.Threading;
using Kofrenot.Core.Storage;

namespace Kofrenot.UI.Platform;

/// <summary>
/// Watches a database folder for change files added by another app or the sync tool, and calls
/// back on the UI thread once they stop arriving for a moment (a sync brings many at once).
/// Watchers can miss events on file shares: the main window also refreshes when it is shown.
/// </summary>
public sealed class DatabaseWatcher : IDisposable
{
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(500);

    private readonly FileSystemWatcher _watcher;
    private readonly DispatcherTimer _timer;

    /// <param name="changed">Called on the thread that created the watcher (the UI thread).</param>
    public DatabaseWatcher(string folder, Action changed)
    {
        _timer = new DispatcherTimer { Interval = Settle };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            changed();
        };

        // Files are written as .tmp, then renamed: the rename is the event that matters.
        _watcher = new FileSystemWatcher(folder, "*" + DatabaseFolder.ChangeExtension)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
        };
        var dispatcher = _timer.Dispatcher;
        void Restart() => dispatcher.BeginInvoke(() =>
        {
            _timer.Stop();
            _timer.Start();
        });
        _watcher.Created += (_, _) => Restart();
        _watcher.Renamed += (_, _) => Restart();
        _watcher.Changed += (_, _) => Restart();
        // Too many events at once (the buffer overflowed): a refresh reads whatever is new anyway.
        _watcher.Error += (_, _) => Restart();
        _watcher.EnableRaisingEvents = true;
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _timer.Stop();
    }
}
