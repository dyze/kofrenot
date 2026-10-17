using System.Diagnostics;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Platform;

/// <summary>Opens web pages in the default browser.</summary>
public static class Browser
{
    /// <summary>Opens <paramref name="url"/>. Returns why it failed, or null.</summary>
    public static string? Open(string url, ILog log)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            log.Info("Action", $"Open link: {url}");
            return null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            log.Error("Action", $"Cannot open {url}", ex);
            return ex.Message;
        }
    }
}
