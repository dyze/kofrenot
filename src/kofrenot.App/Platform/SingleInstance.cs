using System.ComponentModel;
using System.Diagnostics;
using Kofrenot.Core;

namespace Kofrenot.UI.Platform;

/// <summary>
/// Keeps a single kofrenot running per Windows session. The first copy owns a named mutex and
/// waits on a named event; a later copy sets the event (the first copy then shows its main
/// window) and exits, so two copies never write the same data files. Which copy is running,
/// and whether to show it, is up to the caller (<see cref="ShowRunning"/>).
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\kofrenot-single-instance";
    private const string ShowEventName = @"Local\kofrenot-show-window";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showEvent;
    private readonly RegisteredWaitHandle _wait;

    private SingleInstance(Mutex mutex, EventWaitHandle showEvent, Action onShowRequested)
    {
        _mutex = mutex;
        _showEvent = showEvent;
        _wait = ThreadPool.RegisterWaitForSingleObject(
            showEvent,
            (_, _) => onShowRequested(),
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    /// <summary>
    /// Returns the instance guard when this is the first copy. Returns null when another copy
    /// is already running.
    /// <paramref name="onShowRequested"/> is called on a background thread.
    /// </summary>
    public static SingleInstance? TryClaim(Action onShowRequested)
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return null;
        }

        var showEvent = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, ShowEventName);
        return new SingleInstance(mutex, showEvent, onShowRequested);
    }

    /// <summary>Asks the copy already running to show its main window.</summary>
    public static void ShowRunning()
    {
        if (EventWaitHandle.TryOpenExisting(ShowEventName, out var existing))
        {
            using (existing)
            {
                existing.Set();
            }
        }
    }

    /// <summary>
    /// The other kofrenot running in this session (its exe and version), or null when it cannot
    /// be found (renamed exe, or its process cannot be read).
    /// </summary>
    public static AppCopy? FindRunning()
    {
        using var current = Process.GetCurrentProcess();
        foreach (var process in Process.GetProcessesByName(current.ProcessName))
        {
            using (process)
            {
                try
                {
                    if (process.Id != current.Id && process.MainModule?.FileName is { } path)
                    {
                        return new AppCopy(path, AppVersion.Format(FileVersionInfo.GetVersionInfo(path).ProductVersion));
                    }
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    // Exited meanwhile, or not readable: try the next one.
                }
            }
        }

        return null;
    }

    public void Dispose()
    {
        _wait.Unregister(null);
        _showEvent.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
