using System.Windows;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Views;

/// <summary>Imports, beside Settings: the vault's mappings, each importing a JSON file's records as nodes (<see cref="ImportsWindow"/>).</summary>
public partial class MainWindow
{
    private void ImportsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session.Current is null)
        {
            MessageDialog.Inform(this, "Open a vault first: its mappings are kept in it.");
            return;
        }

        _log.Info("Action", "Imports shown");
        new ImportsWindow(this, _session, Ask, _log).ShowDialog();
    }
}
