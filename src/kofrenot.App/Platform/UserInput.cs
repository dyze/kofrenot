using System.Runtime.InteropServices;

namespace Kofrenot.UI.Platform;

/// <summary>The keyboard and mouse input of the Windows session, in any program.</summary>
public static class UserInput
{
    /// <summary>The time since the last keyboard or mouse input; zero when Windows cannot tell.</summary>
    public static TimeSpan IdleTime()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info))
        {
            return TimeSpan.Zero;
        }

        // Both tick counts wrap around after 49.7 days: their difference stays right.
        return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.Time));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);
}
