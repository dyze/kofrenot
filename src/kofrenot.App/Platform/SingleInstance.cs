using System.ComponentModel;
using System.Diagnostics;
using Kofrenot.Core;

namespace Kofrenot.UI.Platform;

/// <summary>How asking the running copy to quit ended (<see cref="SingleInstance.TryReplace"/>).</summary>
public enum ReplaceOutcome
{
    /// <summary>It quit: this copy now runs.</summary>
    Replaced,

    /// <summary>It refused (a note not saved, an import running) and says why in its window.</summary>
    Refused,

    /// <summary>It did not quit in time.</summary>
    NotClosed,
}

/// <summary>
/// Keeps a single kofrenot running per Windows session. The first copy owns a named mutex and
/// waits on two named events: show (it then shows its main window) and quit (another copy
/// replaces it; a refusal is told through a third event). A later copy sets one of them, so two
/// copies never write the same data files. Which copy is running, and what to ask it, is up to
/// the caller.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\kofrenot-single-instance";
    private const string ShowEventName = @"Local\kofrenot-show-window";
    private const string QuitEventName = @"Local\kofrenot-quit";
    private const string RefusedEventName = @"Local\kofrenot-quit-refused";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showEvent;
    private readonly EventWaitHandle _quitEvent;
    private readonly EventWaitHandle _refusedEvent;
    private readonly RegisteredWaitHandle _showWait;
    private readonly RegisteredWaitHandle _quitWait;

    private SingleInstance(Mutex mutex, Action onShowRequested, Action onQuitRequested)
    {
        _mutex = mutex;
        _showEvent = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, ShowEventName);
        _quitEvent = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, QuitEventName);
        _refusedEvent = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, RefusedEventName);
        _showWait = WaitFor(_showEvent, onShowRequested);
        _quitWait = WaitFor(_quitEvent, onQuitRequested);
    }

    /// <summary>
    /// Returns the instance guard when this is the first copy. Returns null when another copy
    /// is already running. The callbacks are called on a background thread.
    /// </summary>
    public static SingleInstance? TryClaim(Action onShowRequested, Action onQuitRequested)
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return null;
        }

        return new SingleInstance(mutex, onShowRequested, onQuitRequested);
    }

    /// <summary>Whether the running copy can be asked to quit (0.3 and older cannot).</summary>
    public static bool CanAskToQuit()
    {
        if (!EventWaitHandle.TryOpenExisting(QuitEventName, out var quit))
        {
            return false;
        }

        quit.Dispose();
        return true;
    }

    /// <summary>
    /// Asks the running copy to quit, and returns the instance guard once it has. Without a guard,
    /// the outcome says whether it refused or did not quit within <paramref name="timeout"/>.
    /// </summary>
    public static (SingleInstance? Guard, ReplaceOutcome Outcome) TryReplace(
        Action onShowRequested,
        Action onQuitRequested,
        TimeSpan timeout)
    {
        if (!EventWaitHandle.TryOpenExisting(RefusedEventName, out var refused))
        {
            return (null, ReplaceOutcome.NotClosed);
        }

        var mutex = new Mutex(initiallyOwned: false, MutexName);
        using (refused)
        {
            if (!TrySet(QuitEventName))
            {
                mutex.Dispose();
                return (null, ReplaceOutcome.NotClosed);
            }

            int signaled;
            try
            {
                signaled = WaitHandle.WaitAny([mutex, refused], timeout);
            }
            catch (AbandonedMutexException)
            {
                // The running copy ended without releasing it (killed): the mutex is ours.
                signaled = 0;
            }

            if (signaled != 0)
            {
                mutex.Dispose();
                return (null, signaled == 1 ? ReplaceOutcome.Refused : ReplaceOutcome.NotClosed);
            }
        }

        return (new SingleInstance(mutex, onShowRequested, onQuitRequested), ReplaceOutcome.Replaced);
    }

    /// <summary>Asks the copy already running to show its main window.</summary>
    public static void ShowRunning() => TrySet(ShowEventName);

    /// <summary>Tells the copy that asked this one to quit that it will not.</summary>
    public void RefuseQuit() => _refusedEvent.Set();

    /// <summary>
    /// The other kofrenot running in this session (its exe and full build), or null when it cannot
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
                        return new AppCopy(path, FileVersionInfo.GetVersionInfo(path).ProductVersion ?? string.Empty);
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
        _showWait.Unregister(null);
        _quitWait.Unregister(null);
        _showEvent.Dispose();
        _quitEvent.Dispose();
        _refusedEvent.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }

    private static RegisteredWaitHandle WaitFor(EventWaitHandle signal, Action onSignal) =>
        ThreadPool.RegisterWaitForSingleObject(
            signal,
            (_, _) => onSignal(),
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);

    private static bool TrySet(string eventName)
    {
        if (!EventWaitHandle.TryOpenExisting(eventName, out var signal))
        {
            return false;
        }

        using (signal)
        {
            signal.Set();
        }

        return true;
    }
}
