using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Placement;
using Kofrenot.Core.Settings;

namespace Kofrenot.UI.Platform;

/// <summary>What <see cref="WindowPlacer"/> gives back to a window when it is shown again.</summary>
public enum RestoreMode
{
    /// <summary>Its place only (a window of fixed size).</summary>
    Position,

    /// <summary>Its place and size, maximized or not.</summary>
    PositionAndSize,

    /// <summary>Its size only; it opens centered on its owner window (a dialog).</summary>
    SizeOverOwner,
}

/// <summary>
/// Puts windows back where they were left (same monitor, same place, same column widths) and
/// remembers where they are when they are hidden or closed. A window without a saved position
/// opens as its XAML says.
/// Each time a window is shown, or its monitor's scaling changes its size, it is moved back
/// inside the screen if part of it is off.
/// </summary>
public sealed class WindowPlacer
{
    private readonly PlacementStore _store;
    private readonly ILog _log;

    public WindowPlacer(PlacementStore store, ILog log)
    {
        _store = store;
        _log = log;
    }

    /// <summary>
    /// Call before the window is first shown. <paramref name="name"/> is its key in windows.json.
    /// </summary>
    /// <param name="columns">The columns of the window's list, whose widths are kept too.</param>
    public void Attach(Window window, string name, RestoreMode mode, GridView? columns = null)
    {
        ScreenRect? lastNormal = null;

        var saved = _store.Get(name);
        if (columns is not null)
        {
            RestoreColumns(columns, saved?.Columns);
        }

        if (mode == RestoreMode.SizeOverOwner)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.SourceInitialized += (_, _) => PlaceOverOwner(window, name, saved);
        }
        else if (saved is not null)
        {
            // Otherwise WPF centers the window once it is shown, over the restored position.
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.SourceInitialized += (_, _) => Restore(window, name, saved, keepSize: mode == RestoreMode.PositionAndSize);
        }

        // A maximized window is saved with its normal size, so it can be restored to it later.
        void TrackNormalBounds(object? sender, EventArgs e)
        {
            if (window.WindowState == WindowState.Normal && NativeWindows.WindowBounds(Handle(window)) is { } bounds)
            {
                lastNormal = bounds;
            }
        }

        // Every move and new scaling, whatever made it: a move no code of kofrenot asked for shows in the log.
        void LogMove(object? sender, EventArgs e)
        {
            if (NativeWindows.WindowBounds(Handle(window)) is { } bounds)
            {
                _log.Debug("Window", $"{name} moved to {bounds.Left},{bounds.Top} ({bounds.Width}x{bounds.Height})");
            }
        }

        window.LocationChanged += TrackNormalBounds;
        window.LocationChanged += LogMove;
        window.SizeChanged += (sender, e) => TrackNormalBounds(sender, e);
        window.IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is false)
            {
                Save(window, name, lastNormal, columns);
            }
            else
            {
                // Once WPF has finished sizing the window for its monitor.
                window.Dispatcher.BeginInvoke(() => KeepOnScreen(window, name, "shown"), DispatcherPriority.Loaded);
            }
        };
        // WPF also raises it with the same scaling (when the window's content changes, a tab switched):
        // only a new scaling can push the window off screen.
        window.DpiChanged += (_, e) =>
        {
            var (from, to) = (e.OldDpi.PixelsPerInchX, e.NewDpi.PixelsPerInchX);
            if (from == to)
            {
                _log.Debug("Window", $"{name} scaling notice ignored: still {to} dpi");
                return;
            }

            _log.Debug("Window", $"{name} scaling changed from {from} to {to} dpi");
            window.Dispatcher.BeginInvoke(() => KeepOnScreen(window, name, "scaling changed"), DispatcherPriority.Loaded);
        };
        window.Closing += (_, _) => Save(window, name, lastNormal, columns);
    }

    /// <summary>
    /// Moves a window that is partly off screen (dragged past an edge, grown with the monitor's
    /// scaling, monitor gone) back inside the work area of the monitor showing most of it.
    /// A maximized or minimized window is left alone.
    /// </summary>
    /// <param name="why">What asked for it ("shown"), for the log.</param>
    private void KeepOnScreen(Window window, string name, string why)
    {
        var handle = Handle(window);
        var monitors = NativeWindows.Monitors();
        if (!window.IsVisible
            || window.WindowState != WindowState.Normal
            || NativeWindows.WindowBounds(handle) is not { } bounds
            || NativeWindows.VisibleBounds(handle) is not { } frame
            || PlacementRules.KeepVisible(frame, monitors) is not { } visible
            || visible == frame)
        {
            return;
        }

        // The check is on the visible frame (a window snapped to an edge is fine); the move is
        // on the whole window, invisible borders included.
        var kept = new ScreenRect(
            bounds.Left + (visible.Left - frame.Left),
            bounds.Top + (visible.Top - frame.Top),
            bounds.Width + (visible.Width - frame.Width),
            bounds.Height + (visible.Height - frame.Height));
        var monitor = PlacementRules.Capture(frame, monitors)?.Monitor;
        var work = monitors.FirstOrDefault(m => m.DeviceName == monitor)?.WorkArea;
        _log.Debug(
            "Window",
            $"{name} kept on screen ({why}): from {Describe(bounds)} to {Describe(kept)};"
            + $" visible frame {Describe(frame)}, kept in {monitor} work area {(work is null ? "?" : Describe(work))}");
        NativeWindows.SetWindowBounds(handle, kept);
    }

    private static string Describe(ScreenRect rect) => $"{rect.Left},{rect.Top} ({rect.Width}x{rect.Height})";

    /// <summary>
    /// Call just before the main window is shown: puts it in the middle of the monitor under the
    /// mouse, or centered on the mouse, fully on screen, keeping its size. For
    /// <see cref="MainWindowPosition.LastPosition"/>, or a maximized window, nothing changes: it
    /// reopens where it was left (see <see cref="Attach"/>).
    /// </summary>
    public static void PlaceMainWindow(Window window, MainWindowPosition position, ILog log)
    {
        if (position == MainWindowPosition.LastPosition
            || window.WindowState == WindowState.Maximized
            || NativeWindows.CursorPosition() is not { } pointer)
        {
            return;
        }

        // Before the first show the window has no handle yet; WPF would also center it over this position.
        var handle = new WindowInteropHelper(window).EnsureHandle();
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        var monitors = NativeWindows.Monitors();

        // Twice: moved to a monitor with another scaling, WPF resizes the window; the second pass
        // centers it with its new size.
        for (var pass = 0; pass < 2; pass++)
        {
            if (NativeWindows.WindowBounds(handle) is not { } current)
            {
                return;
            }

            var bounds = position == MainWindowPosition.AtMouse
                ? PlacementRules.CenterOnPoint(pointer, current.Width, current.Height, monitors)
                : PlacementRules.CenterOnMonitorAt(pointer, current.Width, current.Height, monitors);
            if (bounds is null)
            {
                return;
            }

            if (bounds != current)
            {
                NativeWindows.SetWindowBounds(handle, bounds);
            }

            if (pass == 1)
            {
                log.Debug("Window", $"Main window placed ({position}) at {bounds.Left},{bounds.Top}");
            }
        }
    }

    /// <summary>
    /// Opens a dialog centered on its owner, at its saved size (else its XAML size), and fully on
    /// screen. Without an owner, it is centered on the screen like any window.
    /// </summary>
    private void PlaceOverOwner(Window window, string name, WindowPlacement? saved)
    {
        var handle = Handle(window);
        if (window.Owner is not { } owner
            || NativeWindows.VisibleBounds(Handle(owner)) is not { } ownerBounds
            || NativeWindows.WindowBounds(handle) is not { } current)
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        var width = saved?.Bounds.Width ?? current.Width;
        var height = saved?.Bounds.Height ?? current.Height;
        var centered = PlacementRules.CenterOn(ownerBounds, width, height);
        var bounds = PlacementRules.KeepVisible(centered, NativeWindows.Monitors()) ?? centered;

        // Set twice: on a monitor with another scaling, the first move makes WPF resize the window.
        NativeWindows.SetWindowBounds(handle, bounds);
        NativeWindows.SetWindowBounds(handle, bounds);
        _log.Debug("Window", $"{name} opened over its owner at {bounds.Left},{bounds.Top} ({bounds.Width}x{bounds.Height})");
    }

    private void Restore(Window window, string name, WindowPlacement saved, bool keepSize)
    {
        var handle = Handle(window);
        if (!keepSize && NativeWindows.WindowBounds(handle) is { } current)
        {
            saved = saved with { Bounds = saved.Bounds with { Width = current.Width, Height = current.Height } };
        }

        if (PlacementRules.Fit(saved, NativeWindows.Monitors()) is not { } bounds)
        {
            return;
        }

        NativeWindows.SetWindowBounds(handle, bounds);
        if (keepSize)
        {
            // On a monitor with another scaling, WPF resizes the window to keep its size in its own
            // units; setting the rectangle again, now that the scaling is right, makes it exact.
            NativeWindows.SetWindowBounds(handle, bounds);
        }

        if (keepSize && saved.Maximized)
        {
            window.WindowState = WindowState.Maximized;
        }

        _log.Debug("Window", $"{name} restored on {saved.Monitor} at {bounds.Left},{bounds.Top} ({bounds.Width}x{bounds.Height})");
    }

    /// <summary>The saved widths, when they still fit the columns (the same number, each of a usable size).</summary>
    private static void RestoreColumns(GridView columns, IReadOnlyList<double>? widths)
    {
        if (widths is null || widths.Count != columns.Columns.Count || widths.Any(width => width is < 16 or > 4000))
        {
            return;
        }

        for (var index = 0; index < widths.Count; index++)
        {
            columns.Columns[index].Width = widths[index];
        }
    }

    private void Save(Window window, string name, ScreenRect? lastNormal, GridView? columns)
    {
        var maximized = window.WindowState == WindowState.Maximized;
        var bounds = maximized ? lastNormal : NativeWindows.WindowBounds(Handle(window)) ?? lastNormal;

        // Minimized, or never shown normal: nothing worth remembering.
        if (bounds is null || window.WindowState == WindowState.Minimized)
        {
            return;
        }

        if (PlacementRules.Capture(bounds, NativeWindows.Monitors(), maximized) is not { } placement)
        {
            return;
        }

        if (columns is not null)
        {
            placement = placement with { Columns = columns.Columns.Select(column => Math.Round(column.ActualWidth, 1)).ToList() };
        }

        try
        {
            _store.Set(name, placement);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _log.Warn("Window", $"Cannot save the position of {name}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static IntPtr Handle(Window window) => new WindowInteropHelper(window).Handle;
}
