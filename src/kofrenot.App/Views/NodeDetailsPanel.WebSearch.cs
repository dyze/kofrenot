using System.Diagnostics;
using System.IO;
using System.Windows;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Views;

/// <summary>
/// The globe, after History: the node's name searched on the web with the engine of Settings > General,
/// in the default browser. The name leaves the computer only then; it is not logged.
/// </summary>
public partial class NodeDetailsPanel
{
    /// <summary>The engine the name is searched with (Settings > General).</summary>
    public Func<WebSearchEngine> WebSearchEngine { get; set; } = () => Core.Browse.WebSearchEngine.Google;

    private void ShowWebSearch(string name) => WebSearchButton.ToolTip = $"Search the web for “{name}”";

    private void WebSearchButton_Click(object sender, RoutedEventArgs e)
    {
        if (Browser?.Details is not { } details || WebSearch.UrlOf(WebSearchEngine(), details.Node.Name) is not { } url)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
            Log.Info("Action", $"Name searched on the web ({WebSearchEngine()})");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            Log.Warn("Action", $"Cannot open the web search: {ex.Message}");
            MessageDialog.Inform(Window.GetWindow(this), $"Cannot open the web search: {ex.Message}");
        }
    }
}
