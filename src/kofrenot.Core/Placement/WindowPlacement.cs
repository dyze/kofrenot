using System.Text.Json.Serialization;

namespace Kofrenot.Core.Placement;

/// <summary>A point in screen pixels, such as the mouse pointer.</summary>
public sealed record ScreenPoint(int X, int Y);

/// <summary>A rectangle in screen pixels.</summary>
public sealed record ScreenRect(int Left, int Top, int Width, int Height)
{
    [JsonIgnore]
    public int Right => Left + Width;

    [JsonIgnore]
    public int Bottom => Top + Height;

    /// <summary>The area shared with <paramref name="other"/>, in square pixels (0 when apart).</summary>
    public long OverlapArea(ScreenRect other)
    {
        var width = Math.Min(Right, other.Right) - Math.Max(Left, other.Left);
        var height = Math.Min(Bottom, other.Bottom) - Math.Max(Top, other.Top);
        return width > 0 && height > 0 ? (long)width * height : 0;
    }

    public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;
}

/// <summary>A monitor as Windows describes it.</summary>
/// <param name="DeviceName">Such as <c>\\.\DISPLAY2</c>: stays the same while the monitor is plugged in the same port.</param>
/// <param name="WorkArea">The monitor without the taskbar, in screen pixels.</param>
/// <param name="IsPrimary">True for the main monitor.</param>
public sealed record MonitorInfo(string DeviceName, ScreenRect WorkArea, bool IsPrimary);

/// <summary>Where a window was left, saved in windows.json.</summary>
/// <param name="Monitor">Device name of the monitor that showed it.</param>
/// <param name="Bounds">Position and size, relative to the top-left corner of that monitor's work area,
/// so that the window keeps its place on the monitor even if the monitors are rearranged.</param>
/// <param name="Maximized">True when the window was maximized; <paramref name="Bounds"/> is then its normal size.</param>
/// <param name="Columns">The widths of its list's columns (the log window), in WPF units; null for a window without.</param>
public sealed record WindowPlacement(string Monitor, ScreenRect Bounds, bool Maximized = false, IReadOnlyList<double>? Columns = null)
{
    // The columns are compared by their widths, not as a list object.
    public bool Equals(WindowPlacement? other) =>
        other is not null
        && Monitor == other.Monitor
        && Bounds == other.Bounds
        && Maximized == other.Maximized
        && (Columns ?? []).SequenceEqual(other.Columns ?? []);

    public override int GetHashCode() => HashCode.Combine(Monitor, Bounds, Maximized, Columns?.Count ?? 0);
}
