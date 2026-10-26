using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Kofrenot.UI.Platform;

/// <summary>Opens a file with the app Windows associates with it.</summary>
public static class FileLauncher
{
    /// <summary>Opens <paramref name="path"/>. Returns why it failed, or null.</summary>
    public static string? Open(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return null;
        }
        catch (Exception ex) when (ex is Win32Exception or IOException)
        {
            return ex.Message;
        }
    }
}
